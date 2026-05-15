namespace SasPortal.Api.Contracts.Auth;

/// <summary>
/// Refresh endpoint accepts an empty body: SAS Portal uses cookie-only auth and reads the
/// refresh token from the HttpOnly refresh cookie. The optional body token is retained only
/// as a defensive fallback and is not part of the supported client contract.
/// </summary>
public sealed record RefreshTokenRequest(
    string? RefreshToken);
