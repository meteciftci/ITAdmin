using System.Security.Claims;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.SystemUpdates;
using ITAdmin.Api.HostAgent;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Security;
using ITAdmin.HostAgent.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/system/updates")]
[Authorize]
public sealed class SystemUpdatesController(
    IHostAgentClient hostAgentClient,
    IAuditLogWriter auditLogWriter,
    ILogger<SystemUpdatesController> logger) : ControllerBase
{
    [HttpGet("status")]
    [RequirePermission(PermissionCodes.SystemUpdates.View)]
    public async Task<ActionResult<SystemUpdateStatusResponse>> GetStatus(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await ReadStatusAsync(checkRepository: true, cancellationToken));
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "ITAdmin Host Agent was unavailable while reading update status.");
            return Ok(UnavailableStatus());
        }
    }

    [HttpPost("check")]
    [RequirePermission(PermissionCodes.SystemUpdates.View)]
    public async Task<ActionResult<SystemUpdateStatusResponse>> CheckForUpdates(
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await ReadStatusAsync(checkRepository: true, cancellationToken);
            return status.RepositoryAccessible
                ? Ok(status)
                : StatusCode(StatusCodes.Status503ServiceUnavailable, status);
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "ITAdmin Host Agent was unavailable while checking for updates.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, UnavailableStatus());
        }
    }

    [HttpPost("install")]
    [RequirePermission(PermissionCodes.SystemUpdates.Manage)]
    public async Task<ActionResult<InstallSystemUpdateResponse>> Install(
        [FromBody] InstallSystemUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.DatabaseBackupConfirmed)
        {
            return BadRequest(new { message = "A current database backup must be confirmed before updating." });
        }

        var targetVersion = request.TargetVersion?.Trim();
        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            return BadRequest(new { message = "targetVersion is required." });
        }

        try
        {
            var current = await ReadStatusAsync(checkRepository: true, cancellationToken);
            if (!current.RepositoryAccessible)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, current);
            }

            if (!current.UpdateAvailable || !string.Equals(
                    targetVersion,
                    current.LatestVersion,
                    StringComparison.Ordinal))
            {
                return BadRequest(new { message = "Only the latest stable release may be installed." });
            }

            if (current.Operation?.Phase is not null
                && IsRunningPhase(current.Operation.Phase))
            {
                return Conflict(new { message = "An update is already in progress." });
            }

            var correlationId = HttpContext.TraceIdentifier;
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.RequestUpdate,
                TargetVersion = targetVersion,
                CorrelationId = correlationId,
            }, cancellationToken);

            if (response.Status is HostAgentResponseStatus.Rejected)
            {
                return Conflict(new { message = response.Message });
            }

            if (response.Status is HostAgentResponseStatus.Denied or HostAgentResponseStatus.Failed)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = response.Message });
            }

            if (response.Status is not HostAgentResponseStatus.Accepted || response.Update?.OperationId is null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The update was not accepted by the host." });
            }

            await auditLogWriter.WriteAsync(new AuditLogWriteRequest
            {
                Action = "SystemUpdateRequested",
                EntityName = "SystemUpdate",
                EntityId = response.Update.OperationId,
                Description = $"ITAdmin update to {targetVersion} was requested.",
                ActorUserId = ResolveActorUserId(),
                ActorUserName = User.Identity?.Name,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
            }, cancellationToken);

            return Accepted(new InstallSystemUpdateResponse(
                response.Update.OperationId,
                targetVersion,
                response.Message));
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "ITAdmin Host Agent was unavailable while requesting an update.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "The ITAdmin Host Agent could not be reached on this server.",
            });
        }
    }

    private async Task<SystemUpdateStatusResponse> ReadStatusAsync(
        bool checkRepository,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        var installation = await hostAgentClient.SendAsync(new HostAgentRequest
        {
            Operation = HostAgentOperation.GetInstallationStatus,
            CorrelationId = correlationId,
        }, cancellationToken);

        var update = await hostAgentClient.SendAsync(new HostAgentRequest
        {
            Operation = HostAgentOperation.GetUpdateStatus,
            CorrelationId = correlationId,
        }, cancellationToken);

        var updateIsRunning = update.Update is not null
            && IsRunningPhase(update.Update.Phase.ToString());
        HostAgentResponse? releases = null;
        if (checkRepository && !updateIsRunning)
        {
            releases = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.CheckForUpdates,
                CorrelationId = correlationId,
            }, cancellationToken);
        }

        var latest = releases?.AvailableReleases?.FirstOrDefault();
        var repositoryAccessible = updateIsRunning
            || releases?.Status is HostAgentResponseStatus.Ok;
        var activeVersion = installation.Installation?.ActiveVersion;
        var latestVersion = latest?.Version ?? (updateIsRunning ? update.Update?.TargetVersion : null);

        return new SystemUpdateStatusResponse(
            AgentAvailable: true,
            RepositoryAccessible: repositoryAccessible,
            RepositoryStatus: updateIsRunning
                ? nameof(HostAgentRepositoryStatus.Verified)
                : releases?.RepositoryStatus.ToString() ?? nameof(HostAgentRepositoryStatus.Unknown),
            Message: repositoryAccessible
                ? releases?.Message ?? "Update status read."
                : releases?.Message ?? "Repository access has not been checked.",
            InstallationPhase: installation.Installation?.Phase,
            ActiveVersion: activeVersion,
            PreviousVersion: installation.Installation?.PreviousVersion,
            Healthy: installation.Installation?.Healthy ?? false,
            LatestVersion: latestVersion,
            LatestSourceCommit: latest?.SourceCommit,
            LatestPublishedAtUtc: latest?.PublishedAtUtc,
            LatestDescription: latest?.Description,
            UpdateAvailable: IsNewer(latestVersion, activeVersion),
            Operation: MapOperation(update.Update),
            CheckedAtUtc: DateTimeOffset.UtcNow);
    }

    private static SystemUpdateOperationResponse? MapOperation(HostAgentUpdateStatus? update) =>
        update is null
            ? null
            : new SystemUpdateOperationResponse(
                update.OperationId,
                update.Phase.ToString(),
                update.TargetVersion,
                update.StartedAtUtc,
                update.CompletedAtUtc,
                update.Message);

    private static bool IsNewer(string? candidate, string? active) =>
        Version.TryParse(candidate, out var candidateVersion)
        && Version.TryParse(active, out var activeVersion)
        && candidateVersion > activeVersion;

    private static bool IsRunningPhase(string phase) => phase is
        nameof(HostAgentUpdatePhase.Resolving)
        or nameof(HostAgentUpdatePhase.Fetching)
        or nameof(HostAgentUpdatePhase.Verifying)
        or nameof(HostAgentUpdatePhase.Staging)
        or nameof(HostAgentUpdatePhase.Migrating)
        or nameof(HostAgentUpdatePhase.Activating);

    private Guid? ResolveActorUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private static SystemUpdateStatusResponse UnavailableStatus() => new(
        AgentAvailable: false,
        RepositoryAccessible: false,
        RepositoryStatus: "HostAgentUnavailable",
        Message: "The ITAdmin Host Agent could not be reached on this server.",
        InstallationPhase: null,
        ActiveVersion: null,
        PreviousVersion: null,
        Healthy: false,
        LatestVersion: null,
        LatestSourceCommit: null,
        LatestPublishedAtUtc: null,
        LatestDescription: null,
        UpdateAvailable: false,
        Operation: null,
        CheckedAtUtc: DateTimeOffset.UtcNow);
}
