namespace ITAdmin.Application.Common.Constants;

public static class SetupPreflightMessageKeys
{
    private const string Prefix = "setupPreflight.";

    public const string DatabaseReachable = Prefix + "databaseReachable";
    public const string DatabaseUnreachable = Prefix + "databaseUnreachable";
    public const string DatabaseQuerySucceeded = Prefix + "databaseQuerySucceeded";
    public const string DatabaseQueryFailed = Prefix + "databaseQueryFailed";
    public const string JwtKeyConfigured = Prefix + "jwtKeyConfigured";
    public const string JwtKeyMissing = Prefix + "jwtKeyMissing";
    public const string JwtIssuerConfigured = Prefix + "jwtIssuerConfigured";
    public const string JwtIssuerMissing = Prefix + "jwtIssuerMissing";
    public const string JwtAudienceConfigured = Prefix + "jwtAudienceConfigured";
    public const string JwtAudienceMissing = Prefix + "jwtAudienceMissing";
    public const string SetupKeyHashConfigured = Prefix + "setupKeyHashConfigured";
    public const string SetupKeyHashMissing = Prefix + "setupKeyHashMissing";
    public const string SetupKeyHashValidFormat = Prefix + "setupKeyHashValidFormat";
    public const string SetupKeyHashInvalidFormat = Prefix + "setupKeyHashInvalidFormat";
    public const string DataProtectionApplicationNameConfigured = Prefix + "dataProtectionApplicationNameConfigured";
    public const string DataProtectionApplicationNameMissing = Prefix + "dataProtectionApplicationNameMissing";
    public const string DataProtectionKeysPathConfigured = Prefix + "dataProtectionKeysPathConfigured";
    public const string DataProtectionKeysPathMissing = Prefix + "dataProtectionKeysPathMissing";
    public const string DataProtectionKeysPathExists = Prefix + "dataProtectionKeysPathExists";
    public const string DataProtectionKeysPathMissingOnDisk = Prefix + "dataProtectionKeysPathMissingOnDisk";
    public const string DataProtectionKeysPathWritable = Prefix + "dataProtectionKeysPathWritable";
    public const string DataProtectionKeysPathNotWritable = Prefix + "dataProtectionKeysPathNotWritable";
    public const string EnvironmentNameAvailable = Prefix + "environmentNameAvailable";
    public const string EnvironmentNameMissing = Prefix + "environmentNameMissing";
    public const string ApplicationNameAvailable = Prefix + "applicationNameAvailable";
    public const string ApplicationNameMissing = Prefix + "applicationNameMissing";
    public const string ApplicationVersionAvailable = Prefix + "applicationVersionAvailable";
    public const string ApplicationVersionMissing = Prefix + "applicationVersionMissing";
}
