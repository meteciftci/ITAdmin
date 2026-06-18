namespace ITAdmin.Api.Security;

/// <summary>
/// Resolves refresh token from JSON body first (backward compatible), then from the HttpOnly cookie.
/// </summary>
public static class AuthRefreshTokenResolution
{
    public static string? ResolveFromBodyFirstThenCookie(string? bodyRefreshToken, string? cookieRefreshToken)
    {
        if (!string.IsNullOrWhiteSpace(bodyRefreshToken))
        {
            return bodyRefreshToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cookieRefreshToken))
        {
            return cookieRefreshToken.Trim();
        }

        return null;
    }
}
