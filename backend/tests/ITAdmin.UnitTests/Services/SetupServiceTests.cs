using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.Services;

public sealed class SetupServiceTests
{
    [Fact]
    public void BuildLdapUserNameCandidates_WithDomainSlashUser_ReturnsOriginalThenSamAccountName()
    {
        var result = SetupService.BuildLdapUserNameCandidates(@"DOMAIN\user");
        Assert.Equal(new[] { @"DOMAIN\user", "user" }, result);
    }

    [Fact]
    public void BuildLdapUserNameCandidates_WithEmailFormat_ReturnsOriginalThenLocalPart()
    {
        var result = SetupService.BuildLdapUserNameCandidates("user@domain.local");
        Assert.Equal(new[] { "user@domain.local", "user" }, result);
    }

    [Fact]
    public void BuildLdapUserNameCandidates_WithPlainSamAccount_ReturnsSingleEntry()
    {
        var result = SetupService.BuildLdapUserNameCandidates("user");
        Assert.Equal(new[] { "user" }, result);
    }

    [Fact]
    public void BuildLdapUserNameCandidates_TrimsAndIgnoresDuplicatesCaseInsensitive()
    {
        var result = SetupService.BuildLdapUserNameCandidates(@"  DOMAIN\User  ");
        Assert.Equal(new[] { @"DOMAIN\User", "User" }, result);
    }

    [Fact]
    public async Task CompleteSetupAsync_WhenFirstProfileLookupFails_RetriesWithSamAccountCandidate()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = request => request.UserName switch
            {
                @"DOMAIN\mete" => null,
                "mete" => new LdapUserProfile("obj-1", "mete", "Mete From Ldap", "mete@corp.test", null),
                _ => null,
            },
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest(
            setupKey: "setup-secret",
            adminUserName: @"DOMAIN\mete",
            adminPassword: "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        Assert.Equal(2, ldap.GetUserProfileCallCount);

        var user = await context.PortalUsers.SingleAsync();
        Assert.Equal("mete", user.UserName);
        Assert.Equal("Mete From Ldap", user.DisplayName);
        Assert.Equal("mete@corp.test", user.Email);
    }

    [Fact]
    public async Task CompleteSetupAsync_UsesLdapProfileEmail_WhenPresent()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = _ => new LdapUserProfile(
                "obj-2",
                "plain",
                "Plain User",
                "plain@ad.test",
                null),
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", "plain", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        var user = await context.PortalUsers.SingleAsync();
        Assert.Equal("plain@ad.test", user.Email);
    }

    [Fact]
    public async Task CompleteSetupAsync_LeavesEmailNull_WhenLdapProfileHasNoEmail()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = _ => new LdapUserProfile("obj-3", "noemail", "No Email User", null, null),
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", "noemail", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        var user = await context.PortalUsers.SingleAsync();
        Assert.Null(user.Email);
    }

    [Fact]
    public async Task CompleteSetupAsync_RejectsInvalidSetupKey()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService();
        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("wrong-key", "user", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal("Invalid setup key.", result.Message);
    }

    [Fact]
    public async Task CompleteSetupAsync_RejectsMissingSetupKeyHashConfiguration()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService();
        var service = CreateSetupService(context, ldap, setupKeyPlaintext: "setup-secret", includeSetupKeyHash: false);
        var request = CreateMinimalCompleteRequest("setup-secret", "user", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal("Setup key hash is not configured.", result.Message);
    }

    [Fact]
    public async Task CompleteSetupAsync_RejectsInvalidSetupKeyHashFormat()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService();
        var service = CreateSetupService(
            context,
            ldap,
            setupKeyPlaintext: "setup-secret",
            configuredSetupKeyHash: "invalid-hash");
        var request = CreateMinimalCompleteRequest("setup-secret", "user", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal("Setup key hash format is invalid.", result.Message);
    }

    [Fact]
    public async Task CompleteSetupAsync_WorksWithoutNationalIdAttribute()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = _ => new LdapUserProfile("obj-4", "plain", "Plain User", "plain@ad.test", null),
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", "plain", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        Assert.False(await context.ApplicationSettings.AnyAsync(x => x.Key == "Directory:NationalIdAttribute"));
    }

    [Fact]
    public async Task CompleteSetupAsync_WhenProfileLookupFails_ReturnsDetailedEnglishMessage()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = _ => null,
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", "someuser", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal(SetupService.DirectoryUserProfileCouldNotBeLoadedMessage, result.Message);
    }

    private static SetupService CreateSetupService(
        AppDbContext context,
        FakeLdapService ldap,
        string setupKeyPlaintext,
        bool includeSetupKeyHash = true,
        string? configuredSetupKeyHash = null)
    {
        var values = new Dictionary<string, string?>();
        if (includeSetupKeyHash)
        {
            values[SetupKeyHashValidator.ConfigurationKey] =
                configuredSetupKeyHash ?? SetupKeyHashValidator.ComputeConfiguredHash(setupKeyPlaintext);
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new SetupService(
            context,
            ldap,
            new FakeSecretProtector(),
            configuration,
            new SetupKeyHashValidator(),
            NullLogger<SetupService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static CompleteSetupRequest CreateMinimalCompleteRequest(
        string setupKey,
        string adminUserName,
        string adminPassword)
    {
        return new CompleteSetupRequest(
            setupKey,
            new CompleteSetupLdapSettings(
                Name: "Default LDAP",
                Host: "dc01.test",
                BaseDn: "DC=test,DC=local",
                UserSearchBase: "",
                UserSearchFilter: "(&(objectClass=user)(sAMAccountName={0}))",
                BindUserName: "bind",
                BindUserDomain: null,
                BindPassword: "bindpw",
                NationalIdAttribute: null),
            new CompleteSetupAdminUser(adminUserName, adminPassword));
    }
}
