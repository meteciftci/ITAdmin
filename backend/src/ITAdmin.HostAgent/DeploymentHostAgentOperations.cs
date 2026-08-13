using ITAdmin.Deployment;
using ITAdmin.HostAgent.Contracts;
using Microsoft.Extensions.Logging;

namespace ITAdmin.HostAgent;

/// <summary>
/// The privileged operations, implemented against the existing deployment contract.
///
/// <para>
/// Nothing here is a second deployment engine. Installation state is the same
/// <see cref="InstallationState"/> file the installer writes, release identity is the same
/// annotated-tag rule the bootstrap uses, and staging/migration/activation is the same installer -
/// invoked by the agent with arguments the agent builds, never with anything a caller supplied.
/// First install, repair, and update therefore converge on one implementation, and a fix to the
/// activation sequence cannot land in one path and be forgotten in the other.
/// </para>
/// </summary>
public sealed class DeploymentHostAgentOperations(
    HostAgentSettings settings,
    GitReleaseClient gitClient,
    IReleaseUpdateExecutor updateExecutor,
    IWebBindingReconciler bindingReconciler,
    ILogger<DeploymentHostAgentOperations> logger) : IHostAgentOperations
{
    private readonly DeploymentLayout _layout = new(settings.ProgramFilesRoot, settings.ProgramDataRoot);
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    private HostAgentUpdateStatus _updateStatus = new()
    {
        Phase = HostAgentUpdatePhase.Idle,
        Message = "No update has been requested.",
    };

    /// <summary>
    /// Reconciles what the last run of this service left behind.
    ///
    /// <para>
    /// Update progress used to live only in memory, so a service restart part-way through an update
    /// left a machine nobody could classify: the release directory might be half-staged, the schema
    /// might be half-migrated, and nothing on disk said so. The operation is now recorded in the
    /// existing installation state, and this reads it at start-up and decides - by how far the
    /// operation had got - whether it can be forgotten, safely retried, or must wait for a human.
    /// </para>
    /// </summary>
    public void ReconcileInterruptedOperation()
    {
        var state = ReadInstallationState();
        if (!state.HasInterruptedOperation)
        {
            return;
        }

        var operation = state.CurrentOperation!;
        var disposition = operation.Classify();

        logger.LogWarning(
            "A {Kind} operation targeting {Version} was interrupted at stage {Stage}; disposition {Disposition}.",
            operation.Kind,
            operation.TargetVersion ?? "(none)",
            operation.Stage,
            disposition);

        switch (disposition)
        {
            case InterruptedOperationDisposition.SafeToDiscard:
                // Nothing durable changed - the staged copy was a temporary directory.
                ClearOperation();
                _updateStatus = _updateStatus with
                {
                    Phase = HostAgentUpdatePhase.Idle,
                    Message = "A previous update was interrupted before anything changed; it was discarded.",
                };
                break;

            case InterruptedOperationDisposition.RetryFromStart:
                ClearOperation();
                _updateStatus = _updateStatus with
                {
                    Phase = HostAgentUpdatePhase.Failed,
                    TargetVersion = operation.TargetVersion,
                    Message = "A previous update was interrupted while staging. No live change was made; "
                        + "the update can be requested again.",
                };
                break;

            default:
                // The schema or the live site may be partially changed. This is never resumed
                // automatically, and it blocks new update requests until an operator clears it.
                _blockedByInterruptedOperation = true;
                _updateStatus = _updateStatus with
                {
                    Phase = HostAgentUpdatePhase.Failed,
                    TargetVersion = operation.TargetVersion,
                    Message = $"A previous update was interrupted at the {operation.Stage} stage. The "
                        + "database schema or the live site may be partially changed; an administrator "
                        + "must review this host before further updates are accepted.",
                };
                break;
        }
    }

    private bool _blockedByInterruptedOperation;

    public Task<HostAgentResponse> GetInstallationStatusAsync(
        HostAgentRequest request,
        CancellationToken cancellationToken)
    {
        var state = ReadInstallationState();

        return Task.FromResult(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = "Installation status read.",
            CorrelationId = request.CorrelationId,
            Installation = new HostAgentInstallationStatus
            {
                Phase = state.Phase.ToString(),
                ActiveVersion = state.ActiveVersion,
                PreviousVersion = state.PreviousVersion,
                Channel = settings.Channel.ToString().ToLowerInvariant(),
                Healthy = state.Phase is InstallationPhase.Installed && !state.MigrationInFlight,
            },
        });
    }

    public async Task<HostAgentResponse> CheckForUpdatesAsync(
        HostAgentRequest request,
        CancellationToken cancellationToken)
    {
        var lines = await gitClient.ListRemoteTagsAsync(cancellationToken);
        var resolution = ReleaseTagResolver.Resolve(lines, settings.Channel);
        var state = ReadInstallationState();

        if (!resolution.IsResolved)
        {
            return HostAgentResponse.Failed(
                resolution.DescribeFailure(settings.Channel),
                request.CorrelationId);
        }

        var releases = resolution.Candidates
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => new HostAgentAvailableRelease
            {
                Version = candidate.Version.ToString(),
                SourceCommit = candidate.SourceCommit,
                IsInstalled = string.Equals(candidate.Version.ToString(), state.ActiveVersion, StringComparison.Ordinal),
            })
            .ToList();

        return new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = $"Latest {settings.Channel.ToString().ToLowerInvariant()} release is {resolution.Selected.Version}.",
            CorrelationId = request.CorrelationId,
            AvailableReleases = releases,
        };
    }

    public async Task<HostAgentResponse> RequestUpdateAsync(
        HostAgentRequest request,
        CancellationToken cancellationToken)
    {
        if (!settings.UpdatesEnabled)
        {
            return HostAgentResponse.Denied(
                "In-app updates are disabled on this host. An administrator must enable them on the server.",
                request.CorrelationId);
        }

        if (!ReleaseVersion.TryParse(request.TargetVersion, out var requested))
        {
            return HostAgentResponse.Rejected("targetVersion is not a valid release version.", request.CorrelationId);
        }

        // The caller's version is treated as a request, not an instruction: it is only honoured if
        // the agent can independently find an annotated tag for it on the configured channel.
        var lines = await gitClient.ListRemoteTagsAsync(cancellationToken);
        var resolution = ReleaseTagResolver.ResolveExact(lines, requested, settings.Channel);
        if (!resolution.IsResolved)
        {
            return HostAgentResponse.Rejected(
                $"Release {requested} is not published as an annotated "
                + $"{settings.Channel.ToString().ToLowerInvariant()} release tag.",
                request.CorrelationId);
        }

        if (_blockedByInterruptedOperation)
        {
            return HostAgentResponse.Rejected(
                "A previous update on this host was interrupted at a stage that may have left the "
                + "database or the live site partially changed. An administrator must review it before "
                + "further updates are accepted.",
                request.CorrelationId);
        }

        if (!await _updateGate.WaitAsync(0, cancellationToken))
        {
            return HostAgentResponse.Rejected(
                "An update is already in progress on this host.",
                request.CorrelationId);
        }

        var release = resolution.Selected;
        var now = DateTimeOffset.UtcNow;

        _updateStatus = new HostAgentUpdateStatus
        {
            Phase = HostAgentUpdatePhase.Resolving,
            TargetVersion = release.Version.ToString(),
            StartedAtUtc = now,
            Message = "Update accepted.",
        };

        // Recorded BEFORE the work starts, so an interruption at any point is visible on disk to
        // the next start of this service.
        WriteOperation(DeploymentOperation.Start(
            Guid.NewGuid().ToString("N"),
            DeploymentOperationKind.Update,
            release.Version.ToString(),
            now));

        // Deliberately not awaited: an update takes minutes and the pipe call must not hold a
        // connection open for it. Progress is polled via GetUpdateStatus.
        _ = Task.Run(() => RunUpdateAsync(release, CancellationToken.None), CancellationToken.None);

        return new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Accepted,
            Message = $"Update to {release.Version} accepted.",
            CorrelationId = request.CorrelationId,
            Update = _updateStatus,
        };
    }

    public Task<HostAgentResponse> GetUpdateStatusAsync(
        HostAgentRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = "Update status read.",
            CorrelationId = request.CorrelationId,
            Update = _updateStatus,
        });

    public async Task<HostAgentResponse> ReconcileWebBindingsAsync(
        HostAgentRequest request,
        CancellationToken cancellationToken)
    {
        var desired = new WebBindingIntent(
            HostName: string.IsNullOrWhiteSpace(request.HostName) ? null : request.HostName.Trim(),
            EnableHttps: request.EnableHttps ?? false,
            CertificateThumbprint: request.CertificateThumbprint,
            RedirectHttpToHttps: request.RedirectHttpToHttps ?? false,
            SiteName: settings.SiteName);

        var result = await bindingReconciler.ReconcileAsync(desired, cancellationToken);

        return result.Succeeded
            ? HostAgentResponse.Ok(result.Message, request.CorrelationId)
            : HostAgentResponse.Failed(result.Message, request.CorrelationId);
    }

    public async Task<HostAgentResponse> RecycleApplicationPoolAsync(
        HostAgentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bindingReconciler.RecycleApplicationPoolAsync(settings.AppPoolName, cancellationToken);

        return result.Succeeded
            ? HostAgentResponse.Ok(result.Message, request.CorrelationId)
            : HostAgentResponse.Failed(result.Message, request.CorrelationId);
    }

    public void LogOperationFailure(HostAgentOperation operation, Exception exception) =>
        logger.LogError(exception, "Host agent operation {Operation} failed.", operation);

    private async Task RunUpdateAsync(RemoteReleaseTag release, CancellationToken cancellationToken)
    {
        try
        {
            SetUpdatePhase(HostAgentUpdatePhase.Fetching, release, "Fetching the release payload.");

            var stagingRoot = Path.Combine(
                Path.GetTempPath(),
                "itadmin-update-" + Guid.NewGuid().ToString("N"));

            try
            {
                await gitClient.FetchDistributionAsync(release.Version, stagingRoot, cancellationToken);

                SetUpdatePhase(HostAgentUpdatePhase.Verifying, release, "Verifying release identity and integrity.");
                var verification = ReleaseAcquisition.Verify(stagingRoot, release.Version, release.SourceCommit);
                if (!verification.IsAcceptable)
                {
                    // Fail closed. A payload whose identity does not match the tag is never staged,
                    // regardless of how it got onto the distribution ref.
                    FailUpdate(release, "Release verification failed: " + string.Join(" ", verification.Problems));
                    return;
                }

                SetUpdatePhase(HostAgentUpdatePhase.Staging, release, "Staging, migrating, and activating.");
                var execution = await updateExecutor.ApplyAsync(
                    new ReleaseUpdateRequest(release.Version, release.SourceCommit, stagingRoot),
                    cancellationToken);

                if (!execution.Succeeded)
                {
                    FailUpdate(release, execution.Message);
                    return;
                }

                _updateStatus = _updateStatus with
                {
                    Phase = HostAgentUpdatePhase.Completed,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Message = $"ITAdmin {release.Version} is active.",
                };

                AdvanceOperation(DeploymentOperationStage.Completed, $"ITAdmin {release.Version} is active.");
                logger.LogInformation("Update to {Version} completed.", release.Version);
            }
            finally
            {
                TryDeleteDirectory(stagingRoot);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Update to {Version} failed.", release.Version);
            FailUpdate(release, "The update failed on the host. See the ITAdmin Host Agent log.");
        }
        finally
        {
            _updateGate.Release();
        }
    }

    private void SetUpdatePhase(HostAgentUpdatePhase phase, RemoteReleaseTag release, string message)
    {
        _updateStatus = _updateStatus with
        {
            Phase = phase,
            TargetVersion = release.Version.ToString(),
            Message = message,
        };

        AdvanceOperation(
            phase switch
            {
                HostAgentUpdatePhase.Fetching => DeploymentOperationStage.Fetching,
                HostAgentUpdatePhase.Verifying => DeploymentOperationStage.Verifying,
                HostAgentUpdatePhase.Staging => DeploymentOperationStage.Staging,
                HostAgentUpdatePhase.Migrating => DeploymentOperationStage.Migrating,
                HostAgentUpdatePhase.Activating => DeploymentOperationStage.Activating,
                _ => DeploymentOperationStage.Resolving,
            },
            message);
    }

    private void FailUpdate(RemoteReleaseTag release, string message)
    {
        _updateStatus = _updateStatus with
        {
            Phase = HostAgentUpdatePhase.Failed,
            TargetVersion = release.Version.ToString(),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Message = message,
        };

        AdvanceOperation(DeploymentOperationStage.Failed, message);
    }

    // --- Durable operation state ------------------------------------------------------------
    // All three helpers swallow write failures deliberately: losing the ability to record progress
    // must not abort a deployment that is otherwise proceeding. A missing record degrades to the
    // old in-memory behaviour rather than to a failed update.

    private void WriteOperation(DeploymentOperation operation) =>
        MutateState(state => state with { CurrentOperation = operation });

    private void AdvanceOperation(DeploymentOperationStage stage, string message) =>
        MutateState(state => state.CurrentOperation is null
            ? state
            : state with
            {
                CurrentOperation = state.CurrentOperation.Advance(stage, message, DateTimeOffset.UtcNow),
            });

    private void ClearOperation() => MutateState(state => state with { CurrentOperation = null });

    private void MutateState(Func<InstallationState, InstallationState> mutate)
    {
        try
        {
            var path = _layout.InstallationStatePath;
            var current = ReadInstallationState();
            var updated = mutate(current) with { UpdatedAtUtc = DateTimeOffset.UtcNow };

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, updated.ToJson());
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not persist deployment operation state.");
        }
    }

    private InstallationState ReadInstallationState()
    {
        try
        {
            var path = _layout.InstallationStatePath;
            if (!File.Exists(path))
            {
                return InstallationState.Fresh(DateTimeOffset.UtcNow);
            }

            return InstallationState.FromJson(File.ReadAllText(path))
                ?? InstallationState.Fresh(DateTimeOffset.UtcNow);
        }
        catch (IOException)
        {
            return InstallationState.Fresh(DateTimeOffset.UtcNow);
        }
        catch (UnauthorizedAccessException)
        {
            return InstallationState.Fresh(DateTimeOffset.UtcNow);
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not remove the update staging directory.");
        }
    }
}

/// <summary>
/// Applies a verified release to this machine. An interface, so the agent's orchestration is
/// testable and so the single real implementation - which shells out to the canonical installer -
/// is the only place that knows how activation works.
/// </summary>
public interface IReleaseUpdateExecutor
{
    Task<ReleaseUpdateResult> ApplyAsync(ReleaseUpdateRequest request, CancellationToken cancellationToken);
}

public sealed record ReleaseUpdateRequest(
    ReleaseVersion Version,
    string SourceCommit,
    string VerifiedReleaseDirectory);

public sealed record ReleaseUpdateResult(bool Succeeded, string Message);

/// <summary>Applies host/HTTPS settings and performs narrow app-pool control.</summary>
public interface IWebBindingReconciler
{
    Task<ReleaseUpdateResult> ReconcileAsync(WebBindingIntent intent, CancellationToken cancellationToken);

    Task<ReleaseUpdateResult> RecycleApplicationPoolAsync(string appPoolName, CancellationToken cancellationToken);
}

public sealed record WebBindingIntent(
    string? HostName,
    bool EnableHttps,
    string? CertificateThumbprint,
    bool RedirectHttpToHttps,
    string SiteName);
