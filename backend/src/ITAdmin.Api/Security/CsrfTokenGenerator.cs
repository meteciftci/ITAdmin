using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace ITAdmin.Api.Security;

/// <summary>
/// Generates URL-safe CSRF tokens for the double-submit cookie pattern.
/// </summary>
public static class CsrfTokenGenerator
{
    private const int TokenByteLength = 32;

    public static string CreateToken()
    {
        Span<byte> buffer = stackalloc byte[TokenByteLength];
        RandomNumberGenerator.Fill(buffer);
        return WebEncoders.Base64UrlEncode(buffer);
    }
}
