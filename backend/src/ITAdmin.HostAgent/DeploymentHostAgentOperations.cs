using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.HostAgent.Contracts;
using Microsoft.Extensions.Logging;

namespace ITAdmin.HostAgent;

/// <summary>
/// The privileged operations. Not a deployment engine: an update is applied by running the
/// <c>Deploy-ITAdmin.ps1</c> already checked out under <c>&lt;InstallRoot&gt;\src</c>, via the
/// Update Coordinator, with arguments the agent builds from its own configuration. First install
/// and update therefore converge on one script, and a fix to the deployment sequence cannot land in
/// one path and be forgotten in the other.
/// </summary>
public sealed class DeploymentHostAgentOperations(
    HostAgentSettings settings,
    GitSourceClient gitClient,
    IHostDeploymentExecutor executor,
    ILogger<DeploymentHostAgentOperations> logger) : IHostAgentOperations
{
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void ReconcileInterruptedOperation()
    {
        var record = ReadOperation();
        if (record is null || IsTerminal(record.Phase))
        {
            return;
        }

        // A running phase with no live process behind it means the service died mid-update. The
        // build on disk may be half-produced and the schema may be part-migrated; that is an
        // operator-review situation, never a silent retry.
        logger.LogWarning(
            "An update ({OperationId}) targeting {TargetCommit} was in phase {Phase} when the agent last stopped.",
            record.OperationId, record.TargetCommit, record.Phase);

        WriteOperation(record with
        {
            Phase = HostAgentUpdatePhase.RequiresOperatorReview,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Message = "A previous update was interrupted. Review the deployment state and the ITAdmin Host Agent log, "
                      + "then run Deploy-ITAdmin.ps1 on this host to converge.",
        });
    }

    public async Task<HostAgentResponse> GetInstallationStatusAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        var state = ReadDeployState();
        var healthy = await IsLocallyHealthyAsync(cancellationToken);

        return new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = "Installation status read.",
            CorrelationId = request.CorrelationId,
            Installation = new HostAgentInstallationStatus
            {
                Phase = state?.ActiveSha is null ? "NotInstalled" : "Installed",
                ActiveCommit = state?.ActiveSha,
                PreviousCommit = state?.PreviousSha,
                Branch = settings.Branch,
                BuiltAtUtc = TryParseTimestamp(state?.ActiveBuiltAtUtc),
                Healthy = healthy,
            },
        };
    }

    public async Task<HostAgentResponse> CheckForUpdatesAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        var access = await gitClient.DiagnoseAccessAsync(cancellationToken);
        if (!access.IsAccessible)
        {
            return new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Failed,
                Message = access.Message,
                CorrelationId = request.CorrelationId,
                RepositoryStatus = access.Status,
            };
        }

        var availability = await gitClient.GetAvailabilityAsync(cancellationToken);
        return new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = availability.UpToDate
                ? "The deployed build is at the branch tip."
                : $"{availability.CommitsBehind} commit(s) behind {availability.Branch}.",
            CorrelationId = request.CorrelationId,
            RepositoryStatus = HostAgentRepositoryStatus.Verified,
            Availability = availability,
        };
    }

    public async Task<HostAgentResponse> RequestUpdateAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        if (!settings.UpdatesEnabled)
        {
            return HostAgentResponse.Rejected(
                "Repository-backed updates are disabled on this host (updatesEnabled=false in hostagent.json).",
                request.CorrelationId);
        }

        if (!await _updateGate.WaitAsync(0, cancellationToken))
        {
            return HostAgentResponse.Rejected("An update is already being applied.", request.CorrelationId);
        }

        try
        {
            var existing = ReadOperation();
            if (existing is not null && !IsTerminal(existing.Phase))
            {
                return HostAgentResponse.Rejected("An update is already in progress.", request.CorrelationId);
            }
            if (existing?.Phase is HostAgentUpdatePhase.RequiresOperatorReview)
            {
                return HostAgentResponse.Rejected(
                    "A previous update needs operator review before another can start. Run Deploy-ITAdmin.ps1 on this host.",
                    request.CorrelationId);
            }

            var access = await gitClient.DiagnoseAccessAsync(cancellationToken);
            if (!access.IsAccessible)
            {
                return new HostAgentResponse
                {
                    Status = HostAgentResponseStatus.Failed,
                    Message = access.Message,
                    CorrelationId = request.CorrelationId,
                    RepositoryStatus = access.Status,
                };
            }

            var availability = await gitClient.GetAvailabilityAsync(cancellationToken);
            var operationId = Guid.NewGuid().ToString("N");
            WriteOperation(new UpdateOperationRecord
            {
                OperationId = operationId,
                Phase = HostAgentUpdatePhase.Pulling,
                TargetCommit = availability.LatestCommit,
                StartedAtUtc = DateTimeOffset.UtcNow,
                Message = "Handing the update to the Update Coordinator.",
            });

            var handoff = await executor.ApplyUpdateAsync(operationId, cancellationToken);
            if (!handoff.Succeeded)
            {
                WriteOperation(new UpdateOperationRecord
                {
                    OperationId = operationId,
                    Phase = HostAgentUpdatePhase.Failed,
                    TargetCommit = availability.LatestCommit,
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Message = handoff.Message,
                });
                return HostAgentResponse.Failed(handoff.Message, request.CorrelationId);
            }

            return new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Accepted,
                Message = availability.UpToDate
                    ? "Redeploying the current branch tip."
                    : $"Updating to {availability.LatestCommit}: {availability.LatestSubject}",
                CorrelationId = request.CorrelationId,
                Update = ToStatus(ReadOperation()),
            };
        }
        finally
        {
            _updateGate.Release();
        }
    }

    public Task<HostAgentResponse> GetUpdateStatusAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = "Update status read.",
            CorrelationId = request.CorrelationId,
            Update = ToStatus(ReadOperation()),
        });
    }

    public async Task<HostAgentResponse> RecycleApplicationPoolAsync(HostAgentRequest request, CancellationToken cancellationToken)
    {
        var result = await executor.RecycleAppPoolAsync(settings.AppPoolName, cancellationToken);
        return result.Succeeded
            ? HostAgentResponse.Ok(result.Message, request.CorrelationId)
            : HostAgentResponse.Failed(result.Message, request.CorrelationId);
    }

    public void LogOperationFailure(HostAgentOperation operation, Exception exception) =>
        logger.LogError(exception, "{Operation} failed.", operation);

    // ------------------------------------------------------------------------------------------

    private static bool IsTerminal(HostAgentUpdatePhase phase) =>
        phase is HostAgentUpdatePhase.Idle or HostAgentUpdatePhase.Completed
            or HostAgentUpdatePhase.Failed or HostAgentUpdatePhase.RequiresOperatorReview;

    private static HostAgentUpdateStatus ToStatus(UpdateOperationRecord? record)
    {
        if (record is null)
        {
            return new HostAgentUpdateStatus { Phase = HostAgentUpdatePhase.Idle, Message = "No update has been requested." };
        }

        return new HostAgentUpdateStatus
        {
            OperationId = record.OperationId,
            Phase = record.Phase,
            TargetCommit = record.TargetCommit,
            StartedAtUtc = record.StartedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc,
            Message = record.Message,
        };
    }

    private UpdateOperationRecord? ReadOperation()
    {
        try
        {
            var path = settings.UpdateOperationPath;
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<UpdateOperationRecord>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteOperation(UpdateOperationRecord record)
    {
        try
        {
            Directory.CreateDirectory(settings.StateRoot);
            var path = settings.UpdateOperationPath;
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(record, JsonOptions));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not persist the update operation record.");
        }
    }

    private DeployStateRecord? ReadDeployState()
    {
        try
        {
            var path = settings.DeployStatePath;
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<DeployStateRecord>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTimeOffset? TryParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private async Task<bool> IsLocallyHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("http://localhost/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Applies an update to this machine and performs narrow app-pool control. An interface so the
/// agent's orchestration is testable off Windows, and so the single real implementation is the only
/// place that knows the Update Coordinator and IIS are involved.
/// </summary>
public interface IHostDeploymentExecutor
{
    Task<ReleaseUpdateResult> ApplyUpdateAsync(string operationId, CancellationToken cancellationToken);

    Task<ReleaseUpdateResult> RecycleAppPoolAsync(string appPoolName, CancellationToken cancellationToken);
}

public sealed record ReleaseUpdateResult(bool Succeeded, string Message);

/// <summary>What the agent and the Update Coordinator both read and write to track one update.</summary>
public sealed record UpdateOperationRecord
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; init; } = string.Empty;

    [JsonPropertyName("phase")]
    public HostAgentUpdatePhase Phase { get; init; }

    [JsonPropertyName("targetCommit")]
    public string? TargetCommit { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public DateTimeOffset? StartedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTimeOffset? CompletedAtUtc { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>The subset of <c>deploy.json</c> the agent reads. Written by <c>Deploy-ITAdmin.ps1</c>.</summary>
public sealed record DeployStateRecord
{
    [JsonPropertyName("activeSha")]
    public string? ActiveSha { get; init; }

    [JsonPropertyName("previousSha")]
    public string? PreviousSha { get; init; }

    [JsonPropertyName("activeBuiltAtUtc")]
    public string? ActiveBuiltAtUtc { get; init; }

    [JsonPropertyName("branch")]
    public string? Branch { get; init; }

    [JsonPropertyName("lastMigration")]
    public string? LastMigration { get; init; }
}
