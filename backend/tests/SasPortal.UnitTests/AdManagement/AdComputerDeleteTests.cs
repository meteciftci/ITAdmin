using System.Reflection;
using SasPortal.Api.Authorization;
using SasPortal.Api.Controllers;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Persistence.Migrations;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdComputerDeleteTests
{
    [Fact]
    public void ComputersDeletePermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Computers.Delete", AdManagementPermissions.ComputersDelete);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsComputersDelete()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersDelete, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ExistingDeploymentMigration_SeedsComputersDeletePermissionIdempotently()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Persistence/Migrations/20260612140000_SeedAdManagementComputersDeletePermission.cs"));

        Assert.Contains("AdManagement.Computers.Delete", source, StringComparison.Ordinal);
        Assert.Contains("WHERE NOT EXISTS", source, StringComparison.Ordinal);
        Assert.Contains("Administrator", source, StringComparison.Ordinal);
        Assert.Contains(nameof(SeedAdManagementComputersDeletePermission), source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteComputerEndpoint_RequiresComputersDeletePermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.DeleteComputer));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersDelete,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void DeleteComputerEndpoint_InvalidGuid_ReturnsBadRequest()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("DeleteComputer", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementApiMessageKeys.Computers.InvalidComputerId", source, StringComparison.Ordinal);
        Assert.Contains("DeleteAdComputerResponse", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerDeleteService_UsesObjectGuidLookupAndLdapDeleteRequest()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerDelete.cs"));

        Assert.Contains("TryLoadComputerAccountState", source, StringComparison.Ordinal);
        Assert.Contains("new DeleteRequest", source, StringComparison.Ordinal);
        Assert.Contains("AdComputerDeleteSteps.VerifyDeleted", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerDeleteService_BlocksProtectedComputers()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerDelete.cs"));

        Assert.Contains("AdComputerAccountGuard.IsProtectedComputer", source, StringComparison.Ordinal);
        Assert.Contains("AdComputerAccountGuard.ProtectedComputerDeleteMessage", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerDeleteService_WritesOperationLogsWithSnapshots()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerDelete.cs"));

        Assert.Contains("AdManagementOperationTypes.ComputerDelete", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Succeeded", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Failed", source, StringComparison.Ordinal);
        Assert.Contains("BuildComputerDeleteBeforeSnapshot", source, StringComparison.Ordinal);
        Assert.Contains("BuildComputerDeleteAfterSnapshot", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteComputerEndpoint_MapsDirectoryFailuresConsistently()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams)", source, StringComparison.Ordinal);
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
