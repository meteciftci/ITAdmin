namespace ITAdmin.Application.Common.Constants;

public static class SetupApiMessageKeys
{
    private const string Prefix = "apiMessages.setup.";

    public static class Validation
    {
        public const string SetupAlreadyCompleted = Prefix + "setupAlreadyCompleted";
        public const string InvalidRequestBody = Prefix + "invalidRequestBody";
        public const string InvalidSetupRequest = Prefix + "invalidSetupRequest";
        public const string InvalidLdapSettings = Prefix + "invalidLdapSettings";
        public const string InvalidSetupKey = Prefix + "invalidSetupKey";
        public const string SetupKeyHashNotConfigured = Prefix + "setupKeyHashNotConfigured";
        public const string SetupKeyHashInvalidFormat = Prefix + "setupKeyHashInvalidFormat";
        public const string DuplicateAdminUser = Prefix + "duplicateAdminUser";
        public const string AdminUsersRequired = Prefix + "adminUsersRequired";
        public const string AdminUserNotFoundInDirectory = Prefix + "adminUserNotFoundInDirectory";
        public const string AdManagementModuleMissingRequiredFields = Prefix + "adManagementModuleMissingRequiredFields";
    }
}
