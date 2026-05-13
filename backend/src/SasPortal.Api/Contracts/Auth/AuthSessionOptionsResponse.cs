namespace SasPortal.Api.Contracts.Auth;

public sealed record AuthSessionOptionsResponse(
    bool RememberMeEnabled,
    int IdleTimeoutMinutes,
    int IdleWarningSeconds,
    int AccessTokenMinutes);
