using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Security;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class DirectoryOrganizationalUnitLookupControllerTests
{
    [Fact]
    public void Search_RequiresDirectoryOrganizationalUnitsLookupPermission()
    {
        var attribute = typeof(DirectoryOrganizationalUnitLookupController)
            .GetMethod(nameof(DirectoryOrganizationalUnitLookupController.Search))!
            .GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal($"Permission:{PermissionCodes.Directory.OrganizationalUnits.Lookup}", attribute!.Policy);
    }

    [Fact]
    public void Search_DoesNotRequireAdManagementSettingsView()
    {
        var attribute = typeof(DirectoryOrganizationalUnitLookupController)
            .GetMethod(nameof(DirectoryOrganizationalUnitLookupController.Search))!
            .GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotEqual($"Permission:{PermissionCodes.AdManagement.Settings.View}", attribute!.Policy);
        Assert.NotEqual($"Permission:{AdManagementPermissions.OrganizationalUnitsView}", attribute.Policy);
    }

    [Fact]
    public void GetReadiness_RequiresManageRequestsNotAdSettings()
    {
        var attribute = typeof(DirectoryOrganizationalUnitLookupController)
            .GetMethod(nameof(DirectoryOrganizationalUnitLookupController.GetReadiness))!
            .GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal($"Permission:{PermissionCodes.LicenseManagement.ManageRequests}", attribute!.Policy);
    }
}
