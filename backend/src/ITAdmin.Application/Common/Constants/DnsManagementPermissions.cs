using ITAdmin.Application.Common.Security;

namespace ITAdmin.Application.Common.Constants;

public static class DnsManagementPermissions
{
    public const string View = PermissionCodes.DnsManagement.View;
    public const string ManageSettings = PermissionCodes.DnsManagement.ManageSettings;
    public const string ServersView = PermissionCodes.DnsManagement.Servers.View;
    public const string ServersManage = PermissionCodes.DnsManagement.Servers.Manage;
    public const string ServersTestConnection = PermissionCodes.DnsManagement.Servers.TestConnection;
    public const string ZonesView = PermissionCodes.DnsManagement.Zones.View;
    public const string ZonesCreate = PermissionCodes.DnsManagement.Zones.Create;
    public const string ZonesUpdate = PermissionCodes.DnsManagement.Zones.Update;
    public const string ZonesDelete = PermissionCodes.DnsManagement.Zones.Delete;
    public const string RecordsView = PermissionCodes.DnsManagement.Records.View;
    public const string RecordsCreate = PermissionCodes.DnsManagement.Records.Create;
    public const string RecordsUpdate = PermissionCodes.DnsManagement.Records.Update;
    public const string RecordsDelete = PermissionCodes.DnsManagement.Records.Delete;
    public const string Compare = PermissionCodes.DnsManagement.Compare;
    public const string Export = PermissionCodes.DnsManagement.Export;
    public const string Synchronize = PermissionCodes.DnsManagement.Synchronize;
    public const string ManageServerSettings = PermissionCodes.DnsManagement.ManageServerSettings;
    public const string ManageDnssec = PermissionCodes.DnsManagement.ManageDnssec;
    public const string ManagePolicies = PermissionCodes.DnsManagement.ManagePolicies;
    public const string ClearCache = PermissionCodes.DnsManagement.ClearCache;
    public const string ManageScavenging = PermissionCodes.DnsManagement.ManageScavenging;
    public const string ManageNetworkConfiguration = PermissionCodes.DnsManagement.ManageNetworkConfiguration;
    public const string ManageZoneTransfers = PermissionCodes.DnsManagement.ManageZoneTransfers;
    public const string ViewOperationLogs = PermissionCodes.DnsManagement.ViewOperationLogs;
}
