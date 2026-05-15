namespace SasPortal.Api.Contracts.Auth;

/// <summary>
/// API response for <c>POST /api/auth/login</c>.
/// Raw access and refresh tokens are intentionally omitted: tokens are delivered only as
/// HttpOnly cookies (see <see cref="Security.AuthSessionCookies"/>).
/// </summary>
public sealed record LoginResponse(
    bool IsSuccess,
    string Message,
    string? ErrorCode = null);
