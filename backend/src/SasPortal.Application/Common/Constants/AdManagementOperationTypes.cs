namespace SasPortal.Application.Common.Constants;

public static class AdManagementOperationTypes
{
    public const string SettingsUpdated = "SettingsUpdated";
    public const string SettingsValidated = "SettingsValidated";
    public const string AttributeMappingCreated = "AttributeMappingCreated";
    public const string AttributeMappingUpdated = "AttributeMappingUpdated";
    public const string AttributeMappingDeleted = "AttributeMappingDeleted";
    public const string CreateUser = "CreateUser";
    public const string UserUpdate = "UserUpdate";
    public const string UserEnable = "UserEnable";
    public const string UserDisable = "UserDisable";
    public const string UserUnlock = "UserUnlock";
    public const string UserGroupAdd = "UserGroupAdd";
    public const string UserGroupRemove = "UserGroupRemove";
}

public static class AdManagementTargetUserTypes
{
    public const string AdUser = "AdUser";
}

public static class AdManagementOperationStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

public static class AdManagementValidationStatuses
{
    public const string Ok = "Ok";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

public static class AdManagementTargetObjectTypes
{
    public const string AdAttributeMapping = "AdAttributeMapping";
    public const string AdManagementSettings = "AdManagementSettings";
}
