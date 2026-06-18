namespace ITAdmin.Application.Common.Models;

public sealed record SecurityLogFilterOptions(
    IReadOnlyList<string> EventTypes,
    IReadOnlyList<string> Severities);
