namespace SasPortal.Api.Contracts.Auth;

public sealed record LoginResponse(
    bool IsSuccess,
    string Message,
    string? AccessToken,
    string? RefreshToken,
    DateTime? AccessTokenExpiresAt,
    DateTime? RefreshTokenExpiresAt);
