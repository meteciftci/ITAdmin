namespace ITAdmin.Application.Common.Models;

public sealed record AuthSessionOptions(
    bool RememberMeEnabled,
    int IdleTimeoutMinutes,
    int IdleWarningSeconds,
    int AccessTokenMinutes);
