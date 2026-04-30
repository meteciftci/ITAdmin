namespace SasPortal.Api.Contracts.Auth;

public sealed record LogoutRequest(
    string RefreshToken);
