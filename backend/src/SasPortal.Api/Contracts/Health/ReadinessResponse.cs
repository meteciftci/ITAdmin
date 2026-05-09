namespace SasPortal.Api.Contracts.Health;

public sealed record ReadinessResponse(
    string Status,
    bool ApiAvailable,
    bool DatabaseAvailable,
    string Message,
    string? TraceId,
    DateTime CheckedAt);
