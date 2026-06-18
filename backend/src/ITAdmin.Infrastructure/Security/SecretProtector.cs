using Microsoft.AspNetCore.DataProtection;
using ITAdmin.Application.Abstractions.Security;

namespace ITAdmin.Infrastructure.Security;

public sealed class SecretProtector : ISecretProtector
{
    private readonly IDataProtector protector;

    public SecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        protector = dataProtectionProvider.CreateProtector("ITAdmin.Secrets.v1");
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
