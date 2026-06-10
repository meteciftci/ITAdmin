using System.Security.Cryptography.X509Certificates;

namespace SasPortal.Infrastructure.Security;

/// <summary>
/// Loads the Data Protection key-encryption certificate from the Windows certificate store
/// (LocalMachine/My first, then CurrentUser/My). Fails fast with a clear, secret-free message
/// when the configured certificate cannot be found, instead of failing later with an opaque
/// cryptography error on the first protect/unprotect call.
/// </summary>
public static class DataProtectionCertificateLoader
{
    public static X509Certificate2 LoadByThumbprint(string thumbprint)
    {
        var normalizedThumbprint = thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal);

        return FindInStore(StoreLocation.LocalMachine, normalizedThumbprint)
            ?? FindInStore(StoreLocation.CurrentUser, normalizedThumbprint)
            ?? throw new InvalidOperationException(
                "Data Protection certificate configured via 'DataProtection:CertificateThumbprint' " +
                "was not found in the certificate store (LocalMachine/My or CurrentUser/My). " +
                "Install the certificate on the server or clear the configuration value.");
    }

    private static X509Certificate2? FindInStore(StoreLocation location, string thumbprint)
    {
        try
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            var matches = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                validOnly: false);

            return matches.Count > 0 ? matches[0] : null;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            // Store may be unavailable (e.g. non-Windows host); the caller decides whether
            // the certificate is mandatory and produces the explicit error message.
            return null;
        }
    }
}
