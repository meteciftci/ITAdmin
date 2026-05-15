namespace SasPortal.Api.Contracts.Auth;

/// <summary>
/// API response for <c>POST /api/auth/refresh</c>.
/// Raw access and refresh tokens are intentionally omitted: rotated tokens are delivered only
/// as HttpOnly cookies (see <see cref="Security.AuthSessionCookies"/>).
/// </summary>
public sealed record RefreshTokenResponse(
    bool IsSuccess,
    string Message,
    string? ErrorCode = null);
