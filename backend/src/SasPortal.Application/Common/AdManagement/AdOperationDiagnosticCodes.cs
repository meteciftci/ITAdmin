namespace SasPortal.Application.Common.AdManagement;

public static class AdOperationDiagnosticCodes
{
    public const string UserGroupAddFailed = "AD_USER_GROUP_ADD_FAILED";
    public const string UserGroupRemoveFailed = "AD_USER_GROUP_REMOVE_FAILED";
    public const string UserEnableFailed = "AD_USER_ENABLE_FAILED";
    public const string UserDisableFailed = "AD_USER_DISABLE_FAILED";
    public const string UserUnlockFailed = "AD_USER_UNLOCK_FAILED";
    public const string UserCreateFailed = "AD_USER_CREATE_FAILED";
    public const string UserOuMoveFailed = "AD_USER_OU_MOVE_FAILED";
    public const string UserManagerUpdateFailed = "AD_USER_MANAGER_UPDATE_FAILED";
    public const string UserAccountExpirationUpdateFailed = "AD_USER_ACCOUNT_EXPIRATION_UPDATE_FAILED";
    public const string SettingsValidationFailed = "AD_SETTINGS_VALIDATION_FAILED";
    public const string AttributeMappingCreateFailed = "AD_ATTRIBUTE_MAPPING_CREATE_FAILED";
    public const string AttributeMappingUpdateFailed = "AD_ATTRIBUTE_MAPPING_UPDATE_FAILED";
    public const string AttributeMappingDeleteFailed = "AD_ATTRIBUTE_MAPPING_DELETE_FAILED";
    public const string GroupCreateFailed = "AD_GROUP_CREATE_FAILED";
    public const string GroupCreatePreflightFailed = "AD_GROUP_CREATE_PREFLIGHT_FAILED";
    public const string GroupUpdateFailed = "AD_GROUP_UPDATE_FAILED";
    public const string GroupUpdatePreflightFailed = "AD_GROUP_UPDATE_PREFLIGHT_FAILED";
}
