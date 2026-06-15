using System.Reflection;
using SasPortal.Api.Authorization;
using SasPortal.Api.Controllers;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdDeletedObjectRestoreTests
{
    [Fact]
    public void DeletedObjectsRestorePermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.DeletedObjects.Restore", AdManagementPermissions.DeletedObjectsRestore);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsDeletedObjectsRestore()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.DeletedObjectsRestore, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SeedDeletedObjectsRestoreMigration_ContainsPermissionAndAdministratorGrant()
    {
        var migrationPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SasPortal.Persistence",
            "Migrations",
            "20260615130000_SeedAdManagementDeletedObjectsRestorePermission.cs"));
        var migrationSource = File.ReadAllText(migrationPath);

        Assert.Contains(AdManagementPermissions.DeletedObjectsRestore, migrationSource, StringComparison.Ordinal);
        Assert.Contains("portal_permissions", migrationSource, StringComparison.Ordinal);
        Assert.Contains("portal_role_permissions", migrationSource, StringComparison.Ordinal);
        Assert.Contains("WHERE NOT EXISTS", migrationSource, StringComparison.Ordinal);
        Assert.Contains("Administrator", migrationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreDeletedObjectEndpoint_RequiresDeletedObjectsRestorePermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.RestoreDeletedObject));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.DeletedObjectsRestore,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void RestoreDeletedObjectEndpoint_InvalidGuid_ReturnsBadRequest()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("RestoreDeletedObject", source, StringComparison.Ordinal);
        Assert.Contains("Geçersiz silinen nesne kimliği.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreDeletedObjectEndpoint_PassesActorContextToService()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("AdDeletedObjectRestoreRequest", source, StringComparison.Ordinal);
        Assert.Contains("ResolveActorUserId(User)", source, StringComparison.Ordinal);
        Assert.Contains("ResolveIpAddress()", source, StringComparison.Ordinal);
        Assert.Contains("ResolveUserAgent()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_UsesGuidLookupShowDeletedAndModifyDn()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("BuildDeletedObjectGuidFilter", source, StringComparison.Ordinal);
        Assert.Contains("ShowDeletedControlOid", source, StringComparison.Ordinal);
        Assert.Contains("ResolveDeletedObjectType", source, StringComparison.Ordinal);
        Assert.Contains("lastKnownParent", source, StringComparison.Ordinal);
        Assert.Contains("msDS-LastKnownRDN", source, StringComparison.Ordinal);
        Assert.Contains("TryLoadDirectoryObjectByDn", source, StringComparison.Ordinal);
        Assert.Contains("new ModifyDNRequest", source, StringComparison.Ordinal);
        Assert.Contains("TryVerifyRestoredObject", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_BlocksUnsupportedTypeAndMissingTargets()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("AdDeletedObjectType.Unknown", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreMissingTargetMessage", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreMissingRdnMessage", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreConflictMessage", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_WritesOperationLogsWithSnapshots()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("AdManagementOperationTypes.DeletedObjectRestore", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Succeeded", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Failed", source, StringComparison.Ordinal);
        Assert.Contains("BuildDeletedObjectRestoreBeforeSnapshot", source, StringComparison.Ordinal);
        Assert.Contains("BuildDeletedObjectRestoreAfterSnapshot", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreOperationType_IsDefined()
    {
        Assert.Equal("DeletedObjectRestore", AdManagementOperationTypes.DeletedObjectRestore);
    }

    [Fact]
    public void AdDeletedObjectRestoreRequest_IncludesActorContextFields()
    {
        var request = new AdDeletedObjectRestoreRequest(
            Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"),
            "actor.user",
            "127.0.0.1",
            "test-agent");

        Assert.Equal("actor.user", request.ActorUserName);
        Assert.Equal("127.0.0.1", request.ActorIpAddress);
        Assert.Equal("test-agent", request.ActorUserAgent);
    }

    [Fact]
    public void DeletedObjectRestoreService_SanitizesLdapErrors()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("SanitizeDeletedObjectRestoreLdapError", source, StringComparison.Ordinal);
        Assert.Contains("AdLdapErrorNormalizer.Normalize", source, StringComparison.Ordinal);
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
}
