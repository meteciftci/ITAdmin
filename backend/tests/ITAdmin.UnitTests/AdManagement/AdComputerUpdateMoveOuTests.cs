using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdComputerUpdateMoveOuTests
{
    [Fact]
    public void ComputersUpdatePermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Computers.Update", AdManagementPermissions.ComputersUpdate);
    }

    [Fact]
    public void ComputersMoveOuPermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Computers.MoveOu", AdManagementPermissions.ComputersMoveOu);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsComputerUpdateAndMoveOuPermissions()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersUpdate, StringComparison.Ordinal);
        });
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersMoveOu, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ExistingDeploymentMigration_SeedsComputerUpdateAndMoveOuPermissionsIdempotently()
    {
        var migrationSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Persistence/Migrations/20260612130000_SeedAdManagementComputersUpdateMoveOuPermissions.cs"));

        Assert.Contains("AdManagement.Computers.Update", migrationSource, StringComparison.Ordinal);
        Assert.Contains("AdManagement.Computers.MoveOu", migrationSource, StringComparison.Ordinal);
        Assert.Contains("WHERE NOT EXISTS", migrationSource, StringComparison.Ordinal);
        Assert.Contains("r.code = 'Administrator'", migrationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateComputerEndpoint_RequiresComputersUpdatePermission()
    {
        var method = typeof(AdComputersController).GetMethod(nameof(AdComputersController.UpdateComputer));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersUpdate,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void MoveComputerOuEndpoint_RequiresComputersMoveOuPermission()
    {
        var method = typeof(AdComputersController).GetMethod(nameof(AdComputersController.MoveComputerOu));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersMoveOu,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void ComputerMutationEndpoints_InvalidGuid_ReturnsBadRequest()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Api/Controllers/AdManagement/AdComputersController.cs"));

        Assert.Contains("UpdateComputer", source, StringComparison.Ordinal);
        Assert.Contains("MoveComputerOu", source, StringComparison.Ordinal);
        Assert.Contains("!Guid.TryParse(id, out var objectGuid)", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementApiMessageKeys.Computers.InvalidComputerId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerUpdateService_OnlyModifiesDescriptionAttribute()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerUpdate.cs"));

        Assert.Contains("\"description\"", source, StringComparison.Ordinal);
        Assert.Contains("ComputerDescriptionMaxLength = 1024", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeComputerDescription", source, StringComparison.Ordinal);
        Assert.Contains("DirectoryAttributeOperation.Delete", source, StringComparison.Ordinal);
        Assert.Contains("DirectoryAttributeOperation.Replace", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyComputerUserAccountControl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModifyDNRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerUpdateService_HandlesNoChangeDescription()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerUpdate.cs"));

        Assert.Contains("skipped (no changes)", source, StringComparison.Ordinal);
        Assert.Contains("""{"changeStatus":"NoChangesDetected"}""", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerMoveOuService_ValidatesTargetOuAndPreservesRdn()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerMoveOu.cs"));

        Assert.Contains("IsValidTargetOuDistinguishedName", source, StringComparison.Ordinal);
        Assert.Contains("TryLoadOrganizationalUnit", source, StringComparison.Ordinal);
        Assert.Contains("GetRelativeDistinguishedName", source, StringComparison.Ordinal);
        Assert.Contains("ModifyDNRequest", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementApiMessageKeys.Computers.AlreadyInTargetOu", source, StringComparison.Ordinal);
        Assert.Contains("IsEqualOrDescendantOf(targetOuDn, computersSearchBase)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerUpdateAndMoveOuServices_BlockProtectedComputers()
    {
        var updateSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerUpdate.cs"));
        var moveSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerMoveOu.cs"));

        Assert.Contains("AdComputerAccountGuard.IsProtectedComputer", updateSource, StringComparison.Ordinal);
        Assert.Contains("AdComputerAccountGuard.IsProtectedComputer", moveSource, StringComparison.Ordinal);
        Assert.Contains("ProtectedComputerWriteOperationMessage", updateSource, StringComparison.Ordinal);
        Assert.Contains("ProtectedComputerWriteOperationMessage", moveSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerUpdateAndMoveOuServices_WriteOperationLogs()
    {
        var updateSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerUpdate.cs"));
        var moveSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerMoveOu.cs"));

        Assert.Contains("AdManagementOperationTypes.ComputerUpdate", updateSource, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationTypes.ComputerMoveOu", moveSource, StringComparison.Ordinal);
        Assert.Contains("BuildComputerUpdateBeforeSnapshot", updateSource, StringComparison.Ordinal);
        Assert.Contains("BuildComputerOuMoveAfterSnapshot", moveSource, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Succeeded", updateSource, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Failed", moveSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerMutationEndpoints_MapDirectoryFailureKinds()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Api/Controllers/AdManagement/AdComputersController.cs"));

        Assert.Contains("MapComputerOperationActionResult", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.NotFound => NotFound", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.ConnectionFailed => StatusCode", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.NotConfigured", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
