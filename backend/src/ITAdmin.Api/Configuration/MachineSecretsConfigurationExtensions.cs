using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ITAdmin.Api.Configuration;

/// <summary>
/// Loads DPAPI-protected machine secrets written by the Windows installer under
/// <c>%ProgramData%\ITAdmin\secrets\runtime.secrets.dpapi</c> into the configuration keys the
/// application already binds (<c>ConnectionStrings:DefaultConnection</c>, <c>Jwt:Key</c>).
///
/// <para>
/// Non-Windows hosts skip this provider (local development uses user-secrets / env vars). The
/// filename and schema must stay aligned with <c>ITAdmin.Deployment.MachineSecrets</c> — covered
/// by a drift test.
/// </para>
/// </summary>
public static class MachineSecretsConfigurationExtensions
{
    /// <summary>Must match <c>ITAdmin.Deployment.MachineSecrets.ProtectedFileName</c>.</summary>
    public const string ProtectedFileName = "runtime.secrets.dpapi";

    /// <summary>
    /// Optional override for the secrets directory. When unset on Windows, the default ProgramData
    /// layout is used.
    /// </summary>
    public const string SecretsRootEnvironmentVariable = "ITADMIN_Secrets__Root";

    /// <summary>
    /// Configuration key the first-run setup key hash is published under. Must match
    /// <c>SetupKeyHashValidator.ConfigurationKey</c> - covered by a drift test.
    /// </summary>
    public const string SetupKeyHashConfigurationKey = "Setup:SetupKeyHash";

    public static IConfigurationBuilder AddITAdminMachineSecrets(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var secretsRoot = ResolveSecretsRoot();
        if (string.IsNullOrWhiteSpace(secretsRoot))
        {
            return builder;
        }

        builder.Add(new MachineSecretsConfigurationSource(secretsRoot));
        return builder;
    }

    internal static string? ResolveSecretsRoot()
    {
        var configured = Environment.GetEnvironmentVariable(SecretsRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(programData))
        {
            return null;
        }

        return Path.Combine(programData, "ITAdmin", "secrets");
    }
}

internal sealed class MachineSecretsConfigurationSource(string secretsRoot) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new MachineSecretsConfigurationProvider(secretsRoot);
}

internal sealed class MachineSecretsConfigurationProvider : ConfigurationProvider
{
    private readonly string _protectedPath;

    public MachineSecretsConfigurationProvider(string secretsRoot)
    {
        _protectedPath = Path.Combine(secretsRoot, MachineSecretsConfigurationExtensions.ProtectedFileName);
    }

    public override void Load()
    {
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows() || !File.Exists(_protectedPath))
        {
            return;
        }

        try
        {
            var secrets = ReadProtectedSecrets(_protectedPath);
            if (!string.IsNullOrWhiteSpace(secrets.ConnectionString))
            {
                Data["ConnectionStrings:DefaultConnection"] = secrets.ConnectionString;
            }

            if (!string.IsNullOrWhiteSpace(secrets.JwtKey))
            {
                Data["Jwt:Key"] = secrets.JwtKey;
            }

            // Only the hash reaches configuration. The plaintext setup key stays in the protected
            // store for the installer's directory-bootstrap step; the web application must never be
            // able to read a value that would let it re-run first-run setup.
            if (!string.IsNullOrWhiteSpace(secrets.SetupKeyHash))
            {
                Data[MachineSecretsConfigurationExtensions.SetupKeyHashConfigurationKey] = secrets.SetupKeyHash;
            }
        }
        catch (CryptographicException)
        {
            // Unreadable ciphertext is treated as absent; startup fails later if Jwt:Key is missing.
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private static ProtectedSecrets ReadProtectedSecrets(string path)
    {
        var protectedBytes = File.ReadAllBytes(path);
        if (protectedBytes.Length == 0)
        {
            return new ProtectedSecrets(null, null, null);
        }

        var plainBytes = ProtectedData.Unprotect(
            protectedBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.LocalMachine);

        return ParseProtectedSecrets(Encoding.UTF8.GetString(plainBytes));
    }

    /// <summary>
    /// Parses the decrypted secret payload. Split out from decryption so the mapping from stored
    /// fields to configuration keys is testable on any operating system.
    /// </summary>
    internal static ProtectedSecrets ParseProtectedSecrets(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new ProtectedSecrets(
            root.TryGetProperty("connectionString", out var connectionString) ? connectionString.GetString() : null,
            root.TryGetProperty("jwtKey", out var jwtKey) ? jwtKey.GetString() : null,
            root.TryGetProperty("setupKeyHash", out var setupKeyHash) ? setupKeyHash.GetString() : null);
    }
}

internal sealed record ProtectedSecrets(string? ConnectionString, string? JwtKey, string? SetupKeyHash);
