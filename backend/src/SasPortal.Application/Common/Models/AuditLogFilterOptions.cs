namespace SasPortal.Application.Common.Models;

public sealed record AuditLogFilterOptions(
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> EntityNames);
