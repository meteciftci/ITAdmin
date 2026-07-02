using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

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
    public void DeleteComputerEndpoint_RequiresComputersDeletePermission()
    {
        var method = typeof(AdComputersController).GetMethod(nameof(AdComputersController.DeleteComputer));
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
                "backend/src/ITAdmin.Api/Controllers/AdManagement/AdComputersController.cs"));

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
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerDelete.cs"));

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
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerDelete.cs"));

        Assert.Contains("AdComputerAccountGuard.IsProtectedComputer", source, StringComparison.Ordinal);
        Assert.Contains("AdComputerAccountGuard.ProtectedComputerDeleteMessage", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerDeleteService_WritesOperationLogsWithSnapshots()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerDelete.cs"));

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
                "backend/src/ITAdmin.Api/Controllers/AdManagement/AdComputersController.cs"));

        Assert.Contains("MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams)", source, StringComparison.Ordinal);
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
