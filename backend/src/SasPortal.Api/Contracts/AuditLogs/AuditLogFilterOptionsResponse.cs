namespace SasPortal.Api.Contracts.AuditLogs;

public sealed record AuditLogFilterOptionsResponse(
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> EntityNames);
