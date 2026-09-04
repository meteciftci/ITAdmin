namespace ITAdmin.Api.Contracts.SystemUpdates;

public sealed record SystemUpdateStatusResponse(
    bool AgentAvailable,
    bool RepositoryAccessible,
    string RepositoryStatus,
    string Message,
    string? InstallationPhase,
    string? ActiveCommit,
    string? PreviousCommit,
    string Branch,
    DateTimeOffset? BuiltAtUtc,
    bool Healthy,
    bool UpdateAvailable,
    int CommitsBehind,
    string? LatestCommit,
    string? LatestSubject,
    SystemUpdateOperationResponse? Operation,
    DateTimeOffset CheckedAtUtc);

public sealed record SystemUpdateOperationResponse(
    string? OperationId,
    string Phase,
    string? TargetCommit,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Message);

public sealed record InstallSystemUpdateRequest(bool DatabaseBackupConfirmed);

public sealed record InstallSystemUpdateResponse(string? OperationId, string? TargetCommit, string Message);
