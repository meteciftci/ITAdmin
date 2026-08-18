namespace ITAdmin.Api.Contracts.SystemUpdates;

public sealed record SystemUpdateStatusResponse(
    bool AgentAvailable,
    bool RepositoryAccessible,
    string RepositoryStatus,
    string Message,
    string? InstallationPhase,
    string? ActiveVersion,
    string? PreviousVersion,
    bool Healthy,
    string? LatestVersion,
    string? LatestSourceCommit,
    DateTimeOffset? LatestPublishedAtUtc,
    string? LatestDescription,
    bool UpdateAvailable,
    SystemUpdateOperationResponse? Operation,
    DateTimeOffset CheckedAtUtc);

public sealed record SystemUpdateOperationResponse(
    string? OperationId,
    string Phase,
    string? TargetVersion,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Message);

public sealed record InstallSystemUpdateRequest(string? TargetVersion, bool DatabaseBackupConfirmed);

public sealed record InstallSystemUpdateResponse(string OperationId, string TargetVersion, string Message);
