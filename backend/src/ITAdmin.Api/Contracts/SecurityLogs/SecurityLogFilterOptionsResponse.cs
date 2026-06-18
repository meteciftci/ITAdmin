namespace ITAdmin.Api.Contracts.SecurityLogs;

public sealed record SecurityLogFilterOptionsResponse(
    IReadOnlyList<string> EventTypes,
    IReadOnlyList<string> Severities);
