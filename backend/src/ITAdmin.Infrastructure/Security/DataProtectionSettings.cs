using Microsoft.Extensions.Configuration;

namespace ITAdmin.Infrastructure.Security;

/// <summary>
/// Typed view over the <c>DataProtection</c> configuration section.
/// All values are optional; empty configuration keeps the framework defaults so
/// local development continues to work without any setup.
/// </summary>
public sealed record DataProtectionSettings(
    string ApplicationName,
    string? KeysPath,
    string? CertificateThumbprint)
{
    public const string SectionName = "DataProtection";
    public const string DefaultApplicationName = "ITAdmin";

    public bool PersistKeysToFileSystem => !string.IsNullOrWhiteSpace(KeysPath);

    public bool ProtectKeysWithCertificate => !string.IsNullOrWhiteSpace(CertificateThumbprint);

    public static DataProtectionSettings Load(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        var applicationName = Normalize(section["ApplicationName"]) ?? DefaultApplicationName;
        var keysPath = Normalize(section["KeysPath"]);
        var certificateThumbprint = Normalize(section["CertificateThumbprint"]);

        return new DataProtectionSettings(applicationName, keysPath, certificateThumbprint);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
