namespace ITAdmin.Application.Common.Models;

public sealed record AuthTokenResult(
    bool IsSuccess,
    string Message,
    string? AccessToken,
    string? RefreshToken,
    DateTime? AccessTokenExpiresAt,
    DateTime? RefreshTokenExpiresAt,
    string? ErrorCode = null);
