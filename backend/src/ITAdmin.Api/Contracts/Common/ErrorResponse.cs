namespace ITAdmin.Api.Contracts.Common;

public sealed record ErrorResponse(
    string Message,
    string? Detail,
    int StatusCode,
    string TraceId,
    string? CorrelationId = null);
