using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace SasPortal.Api.Security;

/// <summary>
/// Double-submit CSRF validation helpers used by <see cref="CsrfProtectionMiddleware"/>.
/// Cookie-authenticated unsafe requests under <c>/api</c> must carry <see cref="HeaderName"/>
/// matching <see cref="AuthCsrfCookie.CookieName"/>; Bearer-authenticated and auth-lifecycle
/// endpoints are intentionally exempt (see <see cref="IsExemptPath"/>).
/// </summary>
public static class CsrfProtection
{
    public const string HeaderName = "X-CSRF-TOKEN";

    /// <summary>
    /// Endpoints that must bypass CSRF enforcement. These run before any CSRF cookie is set,
    /// or must remain reachable to clear server-side state regardless of cookie freshness.
    /// </summary>
    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/logout",
    };

    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
    };

    public static bool IsUnsafeMethod(string method) => UnsafeMethods.Contains(method);

    /// <summary>
    /// Returns <c>true</c> for the auth lifecycle endpoints that must remain reachable
    /// without a valid CSRF token (login, refresh, logout).
    /// </summary>
    public static bool IsExemptPath(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        return ExemptPaths.Contains(path.Value!);
    }

    /// <summary>
    /// Returns <c>true</c> when the request authenticates via <c>Authorization: Bearer …</c>.
    /// Bearer credentials are not auto-attached by the browser, so CSRF is unnecessary.
    /// </summary>
    public static bool HasBearerAuthorization(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        return !string.IsNullOrEmpty(authorization)
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> when the request carries the access-token cookie that the browser
    /// attaches automatically and therefore needs CSRF protection.
    /// </summary>
    public static bool HasAccessTokenCookie(HttpRequest request) =>
        request.Cookies.TryGetValue(AuthAccessCookie.CookieName, out var value)
        && !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Decides whether the middleware must validate the CSRF token for this request.
    /// </summary>
    public static bool ShouldValidateRequest(HttpRequest request)
    {
        if (!IsUnsafeMethod(request.Method))
        {
            return false;
        }

        if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsExemptPath(request.Path))
        {
            return false;
        }

        if (HasBearerAuthorization(request))
        {
            return false;
        }

        return HasAccessTokenCookie(request);
    }

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
    /// Validates that the CSRF header matches the CSRF cookie. Used by the middleware
    /// after <see cref="ShouldValidateRequest"/> has identified an enforced request.
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
