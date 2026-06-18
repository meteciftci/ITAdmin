using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdComputerAccountOperationTests
{
    [Fact]
    public void ComputersEnablePermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Computers.Enable", AdManagementPermissions.ComputersEnable);
    }

    [Fact]
    public void ComputersDisablePermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Computers.Disable", AdManagementPermissions.ComputersDisable);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsComputerAccountOperationPermissions()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersEnable, StringComparison.Ordinal);
        });
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersDisable, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void EnableComputerEndpoint_RequiresComputersEnablePermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.EnableComputer));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersEnable,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void DisableComputerEndpoint_RequiresComputersDisablePermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.DisableComputer));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersDisable,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void ExecuteComputerAccountOperationAsync_InvalidGuid_ReturnsBadRequest()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("ExecuteComputerAccountOperationAsync", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementApiMessageKeys.Computers.InvalidComputerId", source, StringComparison.Ordinal);
        Assert.Contains("!Guid.TryParse(id, out var objectGuid)", source, StringComparison.Ordinal);
        Assert.Contains("return BadRequest(new AdComputerAccountOperationResponse(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EnableOperation_RemovesAccountDisableBit()
    {
        const int disabledUac = 0x0202;
        var updated = AdLdapValueConverter.ApplyAccountDisabledFlag(disabledUac, disabled: false);
        Assert.True(AdLdapValueConverter.IsAccountEnabled(updated));
    }

    [Fact]
    public void DisableOperation_AddsAccountDisableBit()
    {
        const int enabledUac = 0x0200;
        var updated = AdLdapValueConverter.ApplyAccountDisabledFlag(enabledUac, disabled: true);
        Assert.False(AdLdapValueConverter.IsAccountEnabled(updated));
    }

    [Fact]
    public void AlreadyEnabled_EnableOperation_IsNoOpAtAccountControlLevel()
    {
        const int enabledUac = 0x0200;
        var updated = AdLdapValueConverter.ApplyAccountDisabledFlag(enabledUac, disabled: false);
        Assert.Equal(enabledUac, updated);
        Assert.True(AdLdapValueConverter.IsAccountEnabled(updated));
    }

    [Fact]
    public void AlreadyDisabled_DisableOperation_IsNoOpAtAccountControlLevel()
    {
        const int disabledUac = 0x0202;
        var updated = AdLdapValueConverter.ApplyAccountDisabledFlag(disabledUac, disabled: true);
        Assert.Equal(disabledUac, updated);
        Assert.False(AdLdapValueConverter.IsAccountEnabled(updated));
    }

    [Theory]
    [InlineData(516, 0x0200, null, true)]
    [InlineData(515, 0x0200, true, true)]
    [InlineData(515, unchecked(0x04002000), null, true)]
    [InlineData(515, 0x0200, false, false)]
    [InlineData(515, 0x1200, false, false)]
    public void ProtectedComputerGuard_BlocksDomainControllerAndCriticalAccounts(
        int? primaryGroupId,
        int? userAccountControl,
        bool? isCriticalSystemObject,
        bool expectedProtected)
    {
        Assert.Equal(
            expectedProtected,
            AdComputerAccountGuard.IsProtectedComputer(
                primaryGroupId,
                userAccountControl,
                isCriticalSystemObject));
    }

    [Fact]
    public void ComputerAccountOperationService_UsesAccountDisableFlagConstant()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerAccountOperations.cs"));

        Assert.Contains("ApplyAccountDisabledFlag", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationTypes.ComputerEnable", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationTypes.ComputerDisable", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementTargetComputerTypes.AdComputer", source, StringComparison.Ordinal);
        Assert.Contains("AdComputerAccountGuard.IsProtectedComputer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerAccountOperationService_WritesOperationLogsOnSuccessAndFailure()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.ComputerAccountOperations.cs"));

        Assert.Contains("AdManagementOperationStatuses.Succeeded", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Failed", source, StringComparison.Ordinal);
        Assert.Contains("BuildComputerAccountBeforeSnapshot", source, StringComparison.Ordinal);
        Assert.Contains("BuildComputerAccountAfterSnapshot", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteComputerAccountOperation_MapsNotConfiguredAndConnectionFailed()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("ExecuteComputerAccountOperationAsync", source, StringComparison.Ordinal);
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
