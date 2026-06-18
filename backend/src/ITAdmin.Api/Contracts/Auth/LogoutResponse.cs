namespace ITAdmin.Api.Contracts.Auth;

public sealed record LogoutResponse(
    bool IsSuccess,
    string Message);
