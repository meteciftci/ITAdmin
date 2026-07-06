using ITAdmin.Application.Common.Security;

namespace ITAdmin.Application.Common.Constants;

public static class LicenseManagementPermissions
{
    public const string View = PermissionCodes.LicenseManagement.View;
    public const string ManageCatalog = PermissionCodes.LicenseManagement.ManageCatalog;
    public const string ManagePurchases = PermissionCodes.LicenseManagement.ManagePurchases;
    public const string ManageRequests = PermissionCodes.LicenseManagement.ManageRequests;
    public const string FulfillRequests = PermissionCodes.LicenseManagement.FulfillRequests;
    public const string ViewReports = PermissionCodes.LicenseManagement.ViewReports;
    public const string ManageSettings = PermissionCodes.LicenseManagement.ManageSettings;
}
