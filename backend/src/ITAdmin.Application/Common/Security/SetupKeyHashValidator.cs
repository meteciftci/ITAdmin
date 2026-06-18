using System.Security.Cryptography;
using System.Text;
using ITAdmin.Application.Abstractions.Security;

namespace ITAdmin.Application.Common.Security;

public sealed class SetupKeyHashValidator : ISetupKeyValidator
{
    public const string ConfigurationKey = "Setup:SetupKeyHash";
    private const string HashPrefix = "sha256:";

    public bool IsHashConfigured(string? configuredHash) =>
        !string.IsNullOrWhiteSpace(configuredHash);

    public bool IsValidHashFormat(string? configuredHash) =>
        TryParseConfiguredHash(configuredHash, out _);

    public SetupKeyValidationOutcome Validate(string? configuredHash, string plaintextSetupKey)
    {
        if (!IsHashConfigured(configuredHash))
        {
            return SetupKeyValidationOutcome.MissingHashConfiguration;
        }

        if (!TryParseConfiguredHash(configuredHash, out var expectedHashBytes))
        {
            return SetupKeyValidationOutcome.InvalidHashFormat;
        }

        if (string.IsNullOrWhiteSpace(plaintextSetupKey))
        {
            return SetupKeyValidationOutcome.InvalidKey;
        }

        var candidateHashBytes = ComputeHashBytes(plaintextSetupKey);
        var isValid = expectedHashBytes.Length == candidateHashBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(expectedHashBytes, candidateHashBytes);

        return isValid
            ? SetupKeyValidationOutcome.Valid
            : SetupKeyValidationOutcome.InvalidKey;
    }

    public static string ComputeConfiguredHash(string plaintextSetupKey)
    {
        var hashBytes = ComputeHashBytes(plaintextSetupKey);
        return HashPrefix + ToBase64Url(hashBytes);
    }

    private static byte[] ComputeHashBytes(string plaintextSetupKey)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(plaintextSetupKey));
    }

    private static bool TryParseConfiguredHash(string? configuredHash, out byte[] hashBytes)
    {
        hashBytes = Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(configuredHash))
        {
            return false;
        }

        var trimmed = configuredHash.Trim();
        if (!trimmed.StartsWith(HashPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var encodedHash = trimmed[HashPrefix.Length..];
        if (encodedHash.Length == 0)
        {
            return false;
        }

        try
        {
            hashBytes = FromBase64Url(encodedHash);
            return hashBytes.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ToBase64Url(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] FromBase64Url(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        return Convert.FromBase64String(base64);
    }
}
