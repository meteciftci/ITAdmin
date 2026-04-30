namespace SasPortal.Api.Contracts.Auth;

public sealed record LoginRequest(
    string UserName,
    string Password);
