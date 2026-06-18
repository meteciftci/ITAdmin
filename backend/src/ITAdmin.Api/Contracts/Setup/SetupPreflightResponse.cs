namespace ITAdmin.Api.Contracts.Setup;

public sealed record SetupPreflightCheckResponse(
    string Key,
    string Status,
    string MessageKey,
    string? Detail);

public sealed record SetupPreflightResponse(
    IReadOnlyList<SetupPreflightCheckResponse> Checks);
