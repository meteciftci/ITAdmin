using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace SasPortal.Api.Security;

/// <summary>
/// Uses the access-token cookie when there is no <c>Authorization: Bearer …</c> header so hybrid auth keeps working.
/// Precedence: Bearer header (default handler), then <see cref="AuthAccessCookie"/>.
/// </summary>
public static class JwtBearerCookieTokenResolver
{
    /// <summary>
    /// Returns the raw JWT from the access cookie when the request does not carry a Bearer authorization value.
    /// </summary>
    public static string? TryGetAccessTokenFromCookieWhenNoBearerHeader(HttpRequest request, string accessCookieName)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (request.Cookies.TryGetValue(accessCookieName, out var cookieValue) &&
            !string.IsNullOrWhiteSpace(cookieValue))
        {
            return cookieValue.Trim();
        }

        return null;
    }

    public static Task OnMessageReceived(MessageReceivedContext context)
    {
        var token = TryGetAccessTokenFromCookieWhenNoBearerHeader(
            context.Request,
            AuthAccessCookie.CookieName);
        if (token is not null)
        {
            context.Token = token;
        }

        return Task.CompletedTask;
    }
}
