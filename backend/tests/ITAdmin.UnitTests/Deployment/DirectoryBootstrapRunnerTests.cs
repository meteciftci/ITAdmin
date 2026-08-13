using ITAdmin.Api;
using ITAdmin.Application.Common.Models;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The installer's directory step. Tested against a fake setup service so the whole contract -
/// bind validation, administrator resolution, idempotency, and what does and does not reach the
/// output - is exercised without a directory, a database, or Windows.
/// </summary>
public sealed class DirectoryBootstrapRunnerTests
{
    private const string BindPassword = "not-a-real-bind-password";

    private static DirectoryBootstrapRequest ValidRequest() => new()
    {
        SetupKey = "generated-setup-key-with-plenty-of-entropy-aaaaaaaaaaaa",
        DirectoryName = "corp.example.com",
        Host = "corp.example.com",
        BaseDn = "DC=corp,DC=example,DC=com",
        UserSearchFilter = "(sAMAccountName={0})",
        BindUserName = "svc_itadmin",
        BindUserDomain = "CORP",
        BindPassword = BindPassword,
        AdministratorIdentifier = "alex@corp.example.com",
    };

    private static SetupAdminUserSearchResult Candidate(
        string userName,
        string? email = null,
        string? objectId = null) =>
        new(userName, userName + " Example", email, $"CN={userName},DC=corp,DC=example,DC=com", objectId ?? userName + "-oid");

    [Fact]
    public async Task Bootstrap_ValidatesTheDirectoryBeforeCreatingAnything()
    {
        var setup = new FakeSetupService
        {
            ValidateLdapResult = new ValidateSetupLdapResult(false, "Directory user authentication failed."),
        };

        var error = new StringWriter();
        var exitCode = await DirectoryBootstrapRunner.ExecuteAsync(setup, ValidRequest(), new StringWriter(), error);

        Assert.Equal(DirectoryBootstrapRunner.DirectoryRejectedExitCode, exitCode);
        Assert.Equal(0, setup.SearchAdminUsersCallCount);
        Assert.Equal(0, setup.CompleteSetupCallCount);
        Assert.Contains("Directory validation failed", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bootstrap_ResolvesTheAdministratorThroughTheValidatedDirectory()
    {
        var setup = new FakeSetupService
        {
            SearchAdminUsersResult = new SearchSetupAdminUsersResult(
                [Candidate("alex", "alex@corp.example.com")]),
        };

        var output = new StringWriter();
        var exitCode = await DirectoryBootstrapRunner.ExecuteAsync(setup, ValidRequest(), output, new StringWriter());

        Assert.Equal(DirectoryBootstrapRunner.SuccessExitCode, exitCode);
        Assert.Equal(1, setup.CompleteSetupCallCount);

        var completed = Assert.Single(setup.LastCompleteSetupRequest!.AdminUsers);
        Assert.Equal("alex", completed.UserName);
        Assert.Equal("alex-oid", completed.DirectoryObjectId);
    }

    [Fact]
    public async Task Bootstrap_UsesTheApplicationsOwnLdapSettingsModel()
    {
        // No parallel installer-only LDAP model: whatever the installer collects is handed to the
        // same setup contract the web wizard uses, so both paths persist identical configuration.
        var setup = new FakeSetupService
        {
            SearchAdminUsersResult = new SearchSetupAdminUsersResult([Candidate("alex")]),
        };

        await DirectoryBootstrapRunner.ExecuteAsync(setup, ValidRequest(), new StringWriter(), new StringWriter());

        var ldap = setup.LastCompleteSetupRequest!.Ldap;
        Assert.Equal("corp.example.com", ldap.Host);
        Assert.Equal("DC=corp,DC=example,DC=com", ldap.BaseDn);
        Assert.Equal("svc_itadmin", ldap.BindUserName);
        Assert.Equal("CORP", ldap.BindUserDomain);
        Assert.Equal("(sAMAccountName={0})", ldap.UserSearchFilter);
    }

    [Fact]
    public async Task Bootstrap_GrantsExactlyOneAdministrator()
    {
        var setup = new FakeSetupService
        {
            SearchAdminUsersResult = new SearchSetupAdminUsersResult([Candidate("alex", "alex@corp.example.com")]),
        };

        await DirectoryBootstrapRunner.ExecuteAsync(setup, ValidRequest(), new StringWriter(), new StringWriter());

        Assert.Single(setup.LastCompleteSetupRequest!.AdminUsers);
    }

    [Fact]
    public async Task Bootstrap_AlreadyComplete_ChangesNothingAndSucceeds()
    {
        // Re-running after a partial failure must not create a second administrator or overwrite a
        // working directory configuration.
        var setup = new FakeSetupService { IsSetupRequiredResult = false };

        var output = new StringWriter();
        var exitCode = await DirectoryBootstrapRunner.ExecuteAsync(setup, ValidRequest(), output, new StringWriter());

        Assert.Equal(DirectoryBootstrapRunner.SuccessExitCode, exitCode);
        Assert.Equal(0, setup.CompleteSetupCallCount);
        Assert.Equal(0, setup.ValidateLdapCallCount);

        var result = DirectoryBootstrapResult.FromJson(output.ToString().Trim());
        Assert.Equal(DirectoryBootstrapStatus.AlreadyBootstrapped, result!.Status);
    }

    [Fact]
    public async Task Bootstrap_AmbiguousAdministrator_IsRefused()
    {
        // Granting administrator to the wrong person is not recoverable by re-running the
        // installer, so ambiguity is never resolved by guessing.
        var setup = new FakeSetupService
        {
            SearchAdminUsersResult = new SearchSetupAdminUsersResult(
                [Candidate("alex.smith"), Candidate("alex.jones")]),
        };

        var error = new StringWriter();
        var request = ValidRequest() with { AdministratorIdentifier = "alex" };

        var exitCode = await DirectoryBootstrapRunner.ExecuteAsync(setup, request, new StringWriter(), error);

        Assert.Equal(DirectoryBootstrapRunner.DirectoryRejectedExitCode, exitCode);
        Assert.Equal(0, setup.CompleteSetupCallCount);
        Assert.Contains("ambiguous", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bootstrap_NoMatchingAdministrator_IsRefusedWithAnActionableMessage()
    {
        var setup = new FakeSetupService
        {
            SearchAdminUsersResult = new SearchSetupAdminUsersResult([]),
        };

        var error = new StringWriter();
        var exitCode = await DirectoryBootstrapRunner.ExecuteAsync(setup, ValidRequest(), new StringWriter(), error);

        Assert.Equal(DirectoryBootstrapRunner.DirectoryRejectedExitCode, exitCode);
        Assert.Contains("Base DN", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SelectAdministrator_ExactUpnWinsOverOtherMatches()
    {
        SetupAdminUserSearchResult[] candidates =
        [
            Candidate("alex.smith"),
            Candidate("alex", "alex@corp.example.com"),
            Candidate("alexandra"),
        ];

        var selected = DirectoryBootstrapRunner.TrySelectAdministrator(
            candidates,
            "alex@corp.example.com",
            out var administrator,
            out _);

        Assert.True(selected);
        Assert.Equal("alex", administrator.UserName);
    }

    [Theory]
    [InlineData("alex")]
    [InlineData("alex@corp.example.com")]
    [InlineData("CORP\\alex")]
    public void SelectAdministrator_AcceptsTheFormsAnOperatorActuallyKnows(string identifier)
    {
        SetupAdminUserSearchResult[] candidates = [Candidate("alex"), Candidate("bailey")];

        Assert.True(DirectoryBootstrapRunner.TrySelectAdministrator(
            candidates,
            identifier,
            out var administrator,
            out _));

        Assert.Equal("alex", administrator.UserName);
    }

    [Fact]
    public void SelectAdministrator_SingleResultIsAcceptedEvenWithoutAnExactMatch()
    {
        // A directory search on a display-name fragment can return one person whose account name
        // does not resemble what was typed; that is not ambiguous.
        Assert.True(DirectoryBootstrapRunner.TrySelectAdministrator(
            [Candidate("asmith")],
            "Alex Smith",
            out var administrator,
            out _));

        Assert.Equal("asmith", administrator.UserName);
    }

    [Fact]
    public async Task Bootstrap_OutputCarriesNoSecretsOrPersonalDirectoryData()
    {
        // The installer prints and logs this verbatim.
        var setup = new FakeSetupService
        {
            SearchAdminUsersResult = new SearchSetupAdminUsersResult(
                [Candidate("alex", "alex@corp.example.com")]),
        };

        var output = new StringWriter();
        await DirectoryBootstrapRunner.ExecuteAsync(setup, ValidRequest(), output, new StringWriter());

        var text = output.ToString();

        Assert.DoesNotContain(BindPassword, text, StringComparison.Ordinal);
        Assert.DoesNotContain("setupKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("svc_itadmin", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alex@corp.example.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CN=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("-oid", text, StringComparison.Ordinal);

        // The one identifying value that is genuinely useful to an operator.
        Assert.Contains("alex", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bootstrap_NeverCreatesALocalPasswordAdministrator()
    {
        var setup = new FakeSetupService
        {
            SearchAdminUsersResult = new SearchSetupAdminUsersResult([Candidate("alex")]),
        };

        await DirectoryBootstrapRunner.ExecuteAsync(setup, ValidRequest(), new StringWriter(), new StringWriter());

        // CompleteSetupAdminUser has no password field at all, and the request carries only the
        // directory identity. ITAdmin authenticates through LDAP; a local-password bootstrap admin
        // would be a permanent second way in.
        var admin = Assert.Single(setup.LastCompleteSetupRequest!.AdminUsers);
        Assert.False(
            admin.GetType().GetProperties().Any(property =>
                property.Name.Contains("password", StringComparison.OrdinalIgnoreCase)));
        Assert.NotNull(admin.DirectoryObjectId);
    }

    [Theory]
    [InlineData("setupKey")]
    [InlineData("host")]
    [InlineData("baseDn")]
    [InlineData("bindUserName")]
    [InlineData("bindPassword")]
    [InlineData("administratorIdentifier")]
    public void Request_MissingRequiredField_IsReported(string field)
    {
        var request = field switch
        {
            "setupKey" => ValidRequest() with { SetupKey = string.Empty },
            "host" => ValidRequest() with { Host = string.Empty },
            "baseDn" => ValidRequest() with { BaseDn = string.Empty },
            "bindUserName" => ValidRequest() with { BindUserName = string.Empty },
            "bindPassword" => ValidRequest() with { BindPassword = string.Empty },
            _ => ValidRequest() with { AdministratorIdentifier = string.Empty },
        };

        Assert.Contains(request.Validate(), problem => problem.Contains(field, StringComparison.Ordinal));
    }

    [Fact]
    public void Request_RoundTripsThroughItsFileForm()
    {
        var restored = DirectoryBootstrapRequest.FromJson(
            System.Text.Json.JsonSerializer.Serialize(ValidRequest()));

        Assert.NotNull(restored);
        Assert.Empty(restored!.Validate());
        Assert.Equal("corp.example.com", restored.ToLdapSettings().Host);
    }

    [Fact]
    public void ResolveInputPath_ReadsTheFileArgument()
    {
        Assert.Equal(
            @"C:\ProgramData\ITAdmin\state\bootstrap.json",
            DirectoryBootstrapRunner.ResolveInputPath(
            [
                "--bootstrap-directory", "--input", @"C:\ProgramData\ITAdmin\state\bootstrap.json",
            ]));

        Assert.Null(DirectoryBootstrapRunner.ResolveInputPath(["--bootstrap-directory"]));
    }

    [Fact]
    public void IsRequested_OnlyForTheBootstrapSwitch()
    {
        Assert.True(DirectoryBootstrapRunner.IsRequested(["--bootstrap-directory"]));
        Assert.False(DirectoryBootstrapRunner.IsRequested(["--migrate"]));
        Assert.False(DirectoryBootstrapRunner.IsRequested([]));
    }
}
