namespace ITAdmin.Application.Common.Constants;

public static class AdDeletedObjectRestoreReadinessCheckKeys
{
    public const string ActiveDirectoryPowerShellModule = "ActiveDirectoryPowerShellModule";
    public const string RestoreAdObjectCommand = "RestoreAdObjectCommand";
    public const string AdwsPortConnectivity = "AdwsPortConnectivity";
    public const string RecycleBinFeature = "RecycleBinFeature";
    public const string ServiceAccountAdwsRead = "ServiceAccountAdwsRead";
    public const string PowerShellTimeout = "PowerShellTimeout";
    public const string RestorePermissionNotVerified = "RestorePermissionNotVerified";
    public const string AdManagementSettings = "AdManagementSettings";
}

public static class AdDeletedObjectRestoreReadinessStatuses
{
    public const string Ready = "Ready";
    public const string Warning = "Warning";
    public const string NotReady = "NotReady";
}

public static class AdDeletedObjectRestoreReadinessCheckStatuses
{
    public const string Success = "Success";
    public const string Warning = "Warning";
    public const string Failed = "Failed";
    public const string NotChecked = "NotChecked";
}
