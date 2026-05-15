using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace SasPortal.Api.Security;

/// <summary>
/// Double-submit CSRF validation helpers for a future phase when the SPA sends <see cref="HeaderName"/>.
/// <strong>Not registered in the request pipeline in Sprint 6.1</strong>; only the CSRF cookie is issued.
/// </summary>
public static class CsrfProtection
{
    public const string HeaderName = "X-CSRF-TOKEN";

    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
    };

    public static bool IsUnsafeMethod(string method) => UnsafeMethods.Contains(method);

    public static bool IsValidHeaderAndCookieMatch(string? headerToken, string? cookieToken)
    {
        if (string.IsNullOrWhiteSpace(headerToken) || string.IsNullOrWhiteSpace(cookieToken))
        {
            return false;
        }

        var headerBytes = Encoding.UTF8.GetBytes(headerToken.Trim());
        var cookieBytes = Encoding.UTF8.GetBytes(cookieToken.Trim());
        if (headerBytes.Length != cookieBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(headerBytes, cookieBytes);
    }

    /// <summary>
    /// Validates that the CSRF header matches the CSRF cookie. Intended for middleware use when enforcement is enabled.
    /// </summary>
    public static bool TryValidateRequest(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return false;
        }

        var header = headerValues.Count > 0 ? headerValues[0] : null;
        request.Cookies.TryGetValue(AuthCsrfCookie.CookieName, out var cookie);
        return IsValidHeaderAndCookieMatch(header, cookie);
    }
}
