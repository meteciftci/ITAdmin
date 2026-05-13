namespace SasPortal.Api.Contracts.Auth;

public sealed record RefreshTokenResponse(
    bool IsSuccess,
    string Message,
    string? AccessToken,
    string? RefreshToken,
    DateTime? AccessTokenExpiresAt,
    DateTime? RefreshTokenExpiresAt,
    string? ErrorCode = null);
