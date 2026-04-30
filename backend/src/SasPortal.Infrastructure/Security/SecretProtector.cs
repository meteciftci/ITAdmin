using Microsoft.AspNetCore.DataProtection;
using SasPortal.Application.Abstractions.Security;

namespace SasPortal.Infrastructure.Security;

public sealed class SecretProtector : ISecretProtector
{
    private readonly IDataProtector protector;

    public SecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        protector = dataProtectionProvider.CreateProtector("SasPortal.Secrets.v1");
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        return protector.Protect(plainText);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return string.Empty;
        }

        return protector.Unprotect(protectedText);
    }
}
