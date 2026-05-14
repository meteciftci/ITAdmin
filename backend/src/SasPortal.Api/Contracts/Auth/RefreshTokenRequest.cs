namespace SasPortal.Api.Contracts.Auth;

/// <summary>
/// Body token is optional when the refresh cookie is present (cookie-first migration path).
/// </summary>
public sealed record RefreshTokenRequest(
    string? RefreshToken);
