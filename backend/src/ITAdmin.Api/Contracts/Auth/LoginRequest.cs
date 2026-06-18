namespace ITAdmin.Api.Contracts.Auth;

public sealed record LoginRequest(
    string UserName,
    string Password,
    bool RememberMe = false);
