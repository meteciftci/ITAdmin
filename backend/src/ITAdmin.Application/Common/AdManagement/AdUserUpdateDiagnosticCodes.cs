namespace ITAdmin.Application.Common.AdManagement;

public static class AdUserUpdateDiagnosticCodes
{
    public const string UpdateFailed = "AD_USER_UPDATE_FAILED";
    public const string ValidationFailed = "AD_USER_UPDATE_VALIDATION_FAILED";
    public const string PreflightFailed = "AD_USER_UPDATE_PREFLIGHT_FAILED";
    public const string UpdateFailedRollbackSucceeded = "AD_USER_UPDATE_FAILED_ROLLBACK_SUCCEEDED";
    public const string UpdateFailedRollbackFailed = "AD_USER_UPDATE_FAILED_ROLLBACK_FAILED";
}
