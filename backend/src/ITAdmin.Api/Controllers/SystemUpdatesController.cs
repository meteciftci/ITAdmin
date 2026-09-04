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

/// <summary>
/// Reads and triggers ITAdmin's own update: there is no release/version numbering any more, only
/// "how far behind the configured branch is the deployed build". Installing runs the same
/// Deploy-ITAdmin.ps1 an operator would run on the server, via the ITAdmin Host Agent.
/// </summary>
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
    public async Task<ActionResult<SystemUpdateStatusResponse>> GetStatus(CancellationToken cancellationToken)
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
    public async Task<ActionResult<SystemUpdateStatusResponse>> CheckForUpdates(CancellationToken cancellationToken)
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

        try
        {
            var current = await ReadStatusAsync(checkRepository: true, cancellationToken);
            if (!current.RepositoryAccessible)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, current);
            }

            if (current.Operation is not null && IsRunningPhase(current.Operation.Phase))
            {
                return Conflict(new { message = "An update is already in progress." });
            }

            var correlationId = HttpContext.TraceIdentifier;
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.RequestUpdate,
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

            if (response.Status is not HostAgentResponseStatus.Accepted)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The update was not accepted by the host." });
            }

            var operationId = response.Update?.OperationId;
            var targetCommit = response.Update?.TargetCommit;

            await auditLogWriter.WriteAsync(new AuditLogWriteRequest
            {
                Action = "SystemUpdateRequested",
                EntityName = "SystemUpdate",
                EntityId = operationId,
                Description = $"An ITAdmin update{(targetCommit is null ? string.Empty : $" to {targetCommit}")} was requested.",
                ActorUserId = ResolveActorUserId(),
                ActorUserName = User.Identity?.Name,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
            }, cancellationToken);

            return Accepted(new InstallSystemUpdateResponse(operationId, targetCommit, response.Message));
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

    private async Task<SystemUpdateStatusResponse> ReadStatusAsync(bool checkRepository, CancellationToken cancellationToken)
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

        var updateIsRunning = update.Update is not null && IsRunningPhase(update.Update.Phase.ToString());

        HostAgentResponse? availability = null;
        if (checkRepository && !updateIsRunning)
        {
            availability = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.CheckForUpdates,
                CorrelationId = correlationId,
            }, cancellationToken);
        }

        var repositoryAccessible = updateIsRunning || availability?.Status is HostAgentResponseStatus.Ok;

        return new SystemUpdateStatusResponse(
            AgentAvailable: true,
            RepositoryAccessible: repositoryAccessible,
            RepositoryStatus: updateIsRunning
                ? nameof(HostAgentRepositoryStatus.Verified)
                : availability?.RepositoryStatus.ToString() ?? nameof(HostAgentRepositoryStatus.Unknown),
            Message: repositoryAccessible
                ? availability?.Message ?? "Update status read."
                : availability?.Message ?? "Repository access has not been checked.",
            InstallationPhase: installation.Installation?.Phase,
            ActiveCommit: installation.Installation?.ActiveCommit,
            PreviousCommit: installation.Installation?.PreviousCommit,
            Branch: installation.Installation?.Branch ?? availability?.Availability?.Branch ?? "main",
            BuiltAtUtc: installation.Installation?.BuiltAtUtc,
            Healthy: installation.Installation?.Healthy ?? false,
            UpdateAvailable: availability?.Availability is { UpToDate: false },
            CommitsBehind: availability?.Availability?.CommitsBehind ?? 0,
            LatestCommit: availability?.Availability?.LatestCommit,
            LatestSubject: availability?.Availability?.LatestSubject,
            Operation: MapOperation(update.Update),
            CheckedAtUtc: DateTimeOffset.UtcNow);
    }

    private static SystemUpdateOperationResponse? MapOperation(HostAgentUpdateStatus? update) =>
        update is null
            ? null
            : new SystemUpdateOperationResponse(
                update.OperationId,
                update.Phase.ToString(),
                update.TargetCommit,
                update.StartedAtUtc,
                update.CompletedAtUtc,
                update.Message);

    private static bool IsRunningPhase(string phase) => phase is
        nameof(HostAgentUpdatePhase.Pulling)
        or nameof(HostAgentUpdatePhase.Building)
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
        ActiveCommit: null,
        PreviousCommit: null,
        Branch: "main",
        BuiltAtUtc: null,
        Healthy: false,
        UpdateAvailable: false,
        CommitsBehind: 0,
        LatestCommit: null,
        LatestSubject: null,
        Operation: null,
        CheckedAtUtc: DateTimeOffset.UtcNow);
}
