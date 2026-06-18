namespace ITAdmin.Application.Common.Models;

public sealed record SetupPreflightCheck(
    string Key,
    string Status,
    string MessageKey,
    string? Detail);

public sealed record SetupPreflightResult(
    IReadOnlyList<SetupPreflightCheck> Checks);
