using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace ITAdmin.Api.Security;

/// <summary>
/// Resolves the JWT used by JwtBearer authentication exclusively from the
/// <see cref="AuthAccessCookie"/> HttpOnly cookie. The <c>Authorization</c> header
/// is intentionally ignored: ITAdmin uses full cookie auth and does not accept
/// Bearer tokens.
/// </summary>
public static class JwtBearerCookieTokenResolver
{
    /// <summary>
    /// Returns the raw JWT carried by the access-token cookie, or <c>null</c> when
    /// the cookie is absent or empty.
    /// </summary>
    public static string? TryGetAccessTokenFromCookie(HttpRequest request, string accessCookieName)
    {
        if (request.Cookies.TryGetValue(accessCookieName, out var cookieValue) &&
            !string.IsNullOrWhiteSpace(cookieValue))
        {
            return cookieValue.Trim();
        }

        return null;
    }

    /// <summary>
    /// JwtBearer <c>OnMessageReceived</c> hook that always reads the access token from
    /// the cookie and ignores any <c>Authorization</c> header sent by the client.
    /// </summary>
    public static Task OnMessageReceived(MessageReceivedContext context)
    {
        var token = TryGetAccessTokenFromCookie(
            context.Request,
            AuthAccessCookie.CookieName);
        if (token is not null)
        {
            context.Token = token;
        }

        return Task.CompletedTask;
    }
}
