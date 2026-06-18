using System.Reflection;
using System.Text.Json;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdOrganizationalUnitTests
{
    [Theory]
    [InlineData("AdManagement.OrganizationalUnits.View", nameof(AdManagementPermissions.OrganizationalUnitsView))]
    [InlineData("AdManagement.OrganizationalUnits.Create", nameof(AdManagementPermissions.OrganizationalUnitsCreate))]
    [InlineData("AdManagement.OrganizationalUnits.Update", nameof(AdManagementPermissions.OrganizationalUnitsUpdate))]
    [InlineData("AdManagement.OrganizationalUnits.Move", nameof(AdManagementPermissions.OrganizationalUnitsMove))]
    [InlineData("AdManagement.OrganizationalUnits.Delete", nameof(AdManagementPermissions.OrganizationalUnitsDelete))]
    public void OrganizationalUnitPermissionConstants_AreDefined(string expected, string propertyName)
    {
        var actual = typeof(AdManagementPermissions).GetField(propertyName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null) as string;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsOrganizationalUnitPermissions()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        var codes = permissions.Cast<object>()
            .Select(item => ((ValueTuple<string, string, string>)item!).Item2)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(AdManagementPermissions.OrganizationalUnitsView, codes);
        Assert.Contains(AdManagementPermissions.OrganizationalUnitsCreate, codes);
        Assert.Contains(AdManagementPermissions.OrganizationalUnitsUpdate, codes);
        Assert.Contains(AdManagementPermissions.OrganizationalUnitsMove, codes);
        Assert.Contains(AdManagementPermissions.OrganizationalUnitsDelete, codes);
    }

    [Theory]
    [InlineData(nameof(AdManagementController.ListManageOrganizationalUnits), AdManagementPermissions.OrganizationalUnitsView)]
    [InlineData(nameof(AdManagementController.GetOrganizationalUnitById), AdManagementPermissions.OrganizationalUnitsView)]
    [InlineData(nameof(AdManagementController.CreateOrganizationalUnit), AdManagementPermissions.OrganizationalUnitsCreate)]
    [InlineData(nameof(AdManagementController.RenameOrganizationalUnit), AdManagementPermissions.OrganizationalUnitsUpdate)]
    [InlineData(nameof(AdManagementController.MoveOrganizationalUnit), AdManagementPermissions.OrganizationalUnitsMove)]
    [InlineData(nameof(AdManagementController.DeleteOrganizationalUnit), AdManagementPermissions.OrganizationalUnitsDelete)]
    public void OrganizationalUnitEndpoints_RequireExpectedPermissions(string methodName, string permission)
    {
        var method = typeof(AdManagementController).GetMethod(methodName);
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + permission,
            permissionAttribute?.Policy);
    }

    [Theory]
    [InlineData("DC=corp,DC=local", true)]
    [InlineData("OU=Sales,DC=corp,DC=local", false)]
    [InlineData("CN=Users,DC=corp,DC=local", false)]
    public void DomainNamingContext_IsDetected(string distinguishedName, bool expected)
    {
        Assert.Equal(expected, AdOrganizationalUnitGuard.IsDomainNamingContext(distinguishedName));
    }

    [Theory]
    [InlineData("OU=Sales,DC=corp,DC=local", true)]
    [InlineData("CN=Users,DC=corp,DC=local", false)]
    [InlineData("DC=corp,DC=local", false)]
    [InlineData("OU=Domain Controllers,DC=corp,DC=local", false)]
    public void ManagedOrganizationalUnit_IsDetected(string distinguishedName, bool expected)
    {
        Assert.Equal(expected, AdOrganizationalUnitGuard.IsManagedOrganizationalUnit(distinguishedName));
    }

    [Fact]
    public void InvalidMoveTarget_DetectsSelfAndDescendants()
    {
        const string source = "OU=Child,OU=Parent,DC=corp,DC=local";
        const string descendantTarget = "OU=Grandchild,OU=Child,OU=Parent,DC=corp,DC=local";

        Assert.True(AdOrganizationalUnitGuard.IsInvalidMoveTarget(source, source));
        Assert.True(AdOrganizationalUnitGuard.IsInvalidMoveTarget(source, descendantTarget));
        Assert.False(AdOrganizationalUnitGuard.IsInvalidMoveTarget(source, "OU=Other,DC=corp,DC=local"));
    }

    [Fact]
    public void CanonicalName_BuildsReadablePath()
    {
        var canonical = AdOrganizationalUnitCanonicalNameBuilder.Build(
            "OU=Engineering,OU=Sales,DC=corp,DC=local");

        Assert.Equal("corp.local/Sales/Engineering", canonical);
    }

    [Fact]
    public void OrganizationalUnitCreate_RequestSummary_ContainsOperationAndParent()
    {
        var request = new CreateAdOrganizationalUnitRequest(
            "Engineering",
            "OU=Sales,DC=corp,DC=local",
            Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            "actor.user",
            "127.0.0.1",
            "test-agent");

        var json = AdOrganizationalUnitSnapshotBuilder.BuildCreateRequestSummary(request);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            AdManagementOperationTypes.OrganizationalUnitCreate,
            document.RootElement.GetProperty("operation").GetString());
        Assert.Equal("Engineering", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(
            "OU=Sales,DC=corp,DC=local",
            document.RootElement.GetProperty("parentDistinguishedName").GetString());
    }

    [Fact]
    public void OrganizationalUnitRename_Snapshots_ContainBeforeAndAfter()
    {
        var detail = CreateSampleDetail();
        var beforeJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
            AdManagementOperationTypes.OrganizationalUnitRename,
            detail);
        var afterDetail = detail with
        {
            Name = "Renamed",
            Ou = "Renamed",
            DistinguishedName = "OU=Renamed,OU=Sales,DC=corp,DC=local",
            CanonicalName = "corp.local/Sales/Renamed",
        };
        var afterJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
            AdManagementOperationTypes.OrganizationalUnitRename,
            afterDetail);

        using var beforeDocument = JsonDocument.Parse(beforeJson);
        using var afterDocument = JsonDocument.Parse(afterJson);

        Assert.Equal(
            "Engineering",
            beforeDocument.RootElement.GetProperty("organizationalUnit").GetProperty("name").GetString());
        Assert.Equal(
            "Renamed",
            afterDocument.RootElement.GetProperty("organizationalUnit").GetProperty("name").GetString());
    }

    [Fact]
    public void OrganizationalUnitDelete_FailureDiagnostic_UsesDeleteFailedCode()
    {
        var organizationalUnitId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var diagnosticJson = AdOrganizationalUnitOperationDiagnosticBuilder.BuildDeleteFailureJson(
            "Preflight",
            organizationalUnitId,
            "OU=Engineering,OU=Sales,DC=corp,DC=local",
            AdUserUpdateNormalizedReasons.InvalidRequest,
            "The organizational unit is not empty.");

        var extractedCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson);

        Assert.Equal(AdOperationDiagnosticCodes.OrganizationalUnitDeleteFailed, extractedCode);
    }

    [Fact]
    public void OrganizationalUnitMessageKeys_UseApiMessagesPrefix()
    {
        Assert.StartsWith(
            "apiMessages.",
            AdManagementApiMessageKeys.OrganizationalUnits.NotEmpty,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "apiMessages.",
            AdManagementApiMessageKeys.OrganizationalUnits.ProtectedObject,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrganizationalUnitDirectory_RequiresMinimumSearchBeforeLdapQuery()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.OrganizationalUnitsDirectory.cs"));

        Assert.Contains("AdLdapAttributeCatalog.IsSearchTermValid(query.Search)", source, StringComparison.Ordinal);
        Assert.Contains("TryCountOneLevelEntries", source, StringComparison.Ordinal);
        Assert.Contains("TryCountOrganizationalUnitChildren", source, StringComparison.Ordinal);
        Assert.Contains("DisplayLabel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrganizationalUnitPickerSearch_RequiresMinimumSearchBeforeLdapQuery()
    {
        var userOuSearchSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.Create.cs"));
        var groupOuSearchSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.GroupsCreate.cs"));
        var computerOuSearchSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputersDirectory.cs"));

        Assert.Contains("AdLdapAttributeCatalog.IsSearchTermValid(query.Search)", userOuSearchSource, StringComparison.Ordinal);
        Assert.Contains("AdLdapAttributeCatalog.IsSearchTermValid(query.Search)", groupOuSearchSource, StringComparison.Ordinal);
        Assert.Contains("AdLdapAttributeCatalog.IsSearchTermValid(query.Search)", computerOuSearchSource, StringComparison.Ordinal);
    }

    [Fact]
    public void OrganizationalUnitInfrastructure_ContainsGuardAndMutationPatterns()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.OrganizationalUnitsMutations.cs"));

        Assert.Contains("OrganizationalUnitHasChildren", source, StringComparison.Ordinal);
        Assert.Contains("ModifyDNRequest", source, StringComparison.Ordinal);
        Assert.Contains("AdOrganizationalUnitGuard.IsManagedOrganizationalUnit", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementApiMessageKeys.OrganizationalUnits.NotEmpty", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend", "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved.");
    }

    private static AdOrganizationalUnitDetail CreateSampleDetail() =>
        new(
            "550e8400-e29b-41d4-a716-446655440000",
            "Engineering",
            "Engineering",
            "Engineering",
            "OU=Engineering,OU=Sales,DC=corp,DC=local",
            "OU=Sales,DC=corp,DC=local",
            "corp.local/Sales/Engineering",
            new AdOrganizationalUnitContentSummary(0, 0, 0, 0),
            []);
}
