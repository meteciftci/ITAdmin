using System.Security.Cryptography;
using System.Text;

namespace ITAdmin.Deployment;

/// <summary>
/// Secrets an installation needs but nobody should be asked to choose.
///
/// <para>
/// The only secret an operator legitimately supplies is one that already exists elsewhere: the
/// PostgreSQL password and the directory bind password belong to accounts the organisation owns.
/// Everything else - the JWT signing key, the first-run setup key - exists solely because ITAdmin
/// needs it, so prompting for it only invites a weak, reused, or written-down value. These are
/// generated from the OS CSPRNG at install time and put straight into the DPAPI-protected machine
/// store; the operator never sees them and never needs to.
/// </para>
/// </summary>
public static class GeneratedSecrets
{
    /// <summary>
    /// 384 bits. Comfortably above the 256-bit HMAC-SHA256 block the JWT signing key is used for,
    /// and the same size for every generated secret so there is one number to reason about.
    /// </summary>
    public const int DefaultEntropyBytes = 48;

    /// <summary>Shortest generated secret this product will accept as valid.</summary>
    public const int MinimumEntropyBytes = 32;

    /// <summary>Prefix used for the setup-key hash; must match the application's validator.</summary>
    public const string SetupKeyHashPrefix = "sha256:";

    /// <summary>
    /// A URL-safe base64 secret with <paramref name="entropyBytes"/> bytes of CSPRNG output.
    /// URL-safe so a value can be carried in a header, query, or JSON field without re-encoding
    /// surprises, and so nothing in the pipeline is tempted to "fix" a <c>+</c> or <c>/</c>.
    /// </summary>
    public static string Create(int entropyBytes = DefaultEntropyBytes)
    {
        if (entropyBytes < MinimumEntropyBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entropyBytes),
                entropyBytes,
                $"Generated secrets must carry at least {MinimumEntropyBytes} bytes of entropy.");
        }

        return ToBase64Url(RandomNumberGenerator.GetBytes(entropyBytes));
    }

    /// <summary>
    /// Hashes a setup key into the <c>sha256:&lt;base64url&gt;</c> form the application's
    /// <c>Setup:SetupKeyHash</c> configuration expects. Kept here, in the deployment contract, so
    /// the installer never has to load application assemblies to produce it - and pinned to the
    /// application's own validator by a drift test.
    /// </summary>
    public static string HashSetupKey(string setupKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setupKey);
        return SetupKeyHashPrefix + ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(setupKey)));
    }

    /// <summary>
    /// A cheap sanity check that a stored value was generated rather than typed.
    ///
    /// <para>
    /// This is not an entropy estimator and does not pretend to be one. It exists to catch the
    /// failure modes that actually happen: a placeholder left in a config, a short human-chosen
    /// string, a repeated character, or a value copied between environments. Anything that passes
    /// here is at least long and varied; anything that fails is definitely not CSPRNG output.
    /// </para>
    /// </summary>
    public static bool LooksSufficientlyRandom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        // 32 bytes of base64 is 43 characters; anything shorter cannot carry the minimum entropy.
        if (trimmed.Length < 43)
        {
            return false;
        }

        if (trimmed.Distinct().Count() < 16)
        {
            return false;
        }

        foreach (var placeholder in Placeholders)
        {
            if (trimmed.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static readonly string[] Placeholders =
    [
        "changeme", "change-me", "placeholder", "example", "sample",
        "password", "secret-key", "your-key", "todo", "dummy",
    ];

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
