namespace ITAdmin.Api.Contracts.Health;

public sealed record ReadinessResponse(
    string Status,
    bool ApiAvailable,
    bool DatabaseAvailable,
    bool LdapAvailable,
    string Message,
    string? TraceId,
    DateTime CheckedAt);
