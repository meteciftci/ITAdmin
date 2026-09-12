using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.DnsManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppModels = ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/dns-management")]
[Authorize]
public sealed class DnsManagementAdministrationController(
    IDnsManagementAdministrationService service,
    IDnsServerConnectionTestService connectionTestService,
    IDnsInventorySyncService inventorySyncService,
    IDnsServerSettingsService serverSettingsService) : ControllerBase
{
    [HttpGet("settings")]
    [RequirePermission(DnsManagementPermissions.ManageSettings)]
    public async Task<ActionResult<DnsManagementSettingsResponse>> GetSettings(CancellationToken cancellationToken) =>
        Ok(Map(await service.GetSettingsAsync(cancellationToken)));

    [HttpPut("settings")]
    [RequirePermission(DnsManagementPermissions.ManageSettings)]
    public async Task<ActionResult<DnsManagementSettingsResponse>> UpdateSettings(
        UpdateDnsManagementSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateSettingsAsync(new(
            request.IsEnabled, request.AutomaticSyncEnabled, request.DefaultSyncIntervalMinutes,
            request.HealthCheckIntervalMinutes, request.CommandTimeoutSeconds, request.MaxParallelServers,
            request.SnapshotRetentionDays, request.SyncRecordInventory,
            request.PromptForFullSyncOnComparisonOpen, request.ComparisonSnapshotStaleAfterMinutes,
            DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.IsSuccess && result.Value is not null ? Ok(Map(result.Value)) : BadRequest(new { message = result.Message });
    }

    [HttpGet("credential-profiles")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public async Task<ActionResult<IReadOnlyList<DnsCredentialProfileResponse>>> GetCredentials(CancellationToken cancellationToken) =>
        Ok((await service.GetCredentialProfilesAsync(cancellationToken)).Select(Map));

    [HttpPost("credential-profiles")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public Task<ActionResult<DnsCredentialProfileResponse>> CreateCredential(
        SaveDnsCredentialProfileRequest request, CancellationToken cancellationToken) => SaveCredential(null, request, cancellationToken);

    [HttpPut("credential-profiles/{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public Task<ActionResult<DnsCredentialProfileResponse>> UpdateCredential(
        Guid id, SaveDnsCredentialProfileRequest request, CancellationToken cancellationToken) => SaveCredential(id, request, cancellationToken);

    [HttpDelete("credential-profiles/{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public async Task<IActionResult> DeleteCredential(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.DeleteCredentialProfileAsync(id, DnsManagementActorResolver.Resolve(this), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { message = result.Message });
    }

    [HttpGet("servers")]
    [RequireAnyPermission(DnsManagementPermissions.ServersView, DnsManagementPermissions.ManageServerSettings, DnsManagementPermissions.ClearCache)]
    public async Task<ActionResult<IReadOnlyList<DnsServerResponse>>> GetServers(CancellationToken cancellationToken) =>
        Ok((await service.GetServersAsync(cancellationToken)).Select(Map));

    [HttpPost("servers")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public Task<ActionResult<DnsServerResponse>> CreateServer(SaveDnsServerRequest request, CancellationToken cancellationToken) =>
        SaveServer(null, request, cancellationToken);

    [HttpPut("servers/{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public Task<ActionResult<DnsServerResponse>> UpdateServer(Guid id, SaveDnsServerRequest request, CancellationToken cancellationToken) =>
        SaveServer(id, request, cancellationToken);

    [HttpPost("servers/{id:guid}/test-connection")]
    [RequirePermission(DnsManagementPermissions.ServersTestConnection)]
    public async Task<ActionResult<DnsServerConnectionTestResponse>> TestServerConnection(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await connectionTestService.TestAsync(
            id, DnsManagementActorResolver.Resolve(this), cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(Map(result.Value))
            : BadRequest(new { message = result.Message });
    }

    [HttpPost("servers/{id:guid}/synchronizations")]
    [RequirePermission(DnsManagementPermissions.Synchronize)]
    public async Task<ActionResult<DnsSyncJobResponse>> SynchronizeServer(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await inventorySyncService.EnqueueAsync(
            id, DnsManagementActorResolver.Resolve(this), cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Accepted(Map(result.Value))
            : BadRequest(new { message = result.Message });
    }

    [HttpGet("servers/{id:guid}/server-settings")]
    [RequirePermission(DnsManagementPermissions.ManageServerSettings)]
    public async Task<ActionResult<DnsServerSettingsResponse>> GetServerSettings(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await serverSettingsService.GetAsync(id, cancellationToken);
        return result.Success && result.Settings is not null
            ? Ok(Map(result.Settings))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPut("servers/{id:guid}/server-settings")]
    [RequirePermission(DnsManagementPermissions.ManageServerSettings)]
    public async Task<ActionResult<DnsServerOperationResponse>> UpdateServerSettings(
        Guid id, UpdateDnsServerSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await serverSettingsService.UpdateAsync(new(
            id, request.ForwarderAddresses ?? [], request.ForwarderUseRootHint,
            request.ForwarderTimeoutSeconds, request.ForwarderEnableReordering,
            request.RecursionEnabled, request.RecursionAdditionalTimeoutSeconds,
            request.RecursionRetryIntervalSeconds, request.RecursionTimeoutSeconds,
            request.RecursionSecureResponse, request.ExpectedStateToken,
            DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.Success
            ? Ok(Map(result))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPost("servers/{id:guid}/cache/clear")]
    [RequirePermission(DnsManagementPermissions.ClearCache)]
    public async Task<ActionResult<DnsServerOperationResponse>> ClearServerCache(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await serverSettingsService.ClearCacheAsync(
            id, DnsManagementActorResolver.Resolve(this), cancellationToken);
        return result.Success
            ? Ok(Map(result))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    private async Task<ActionResult<DnsCredentialProfileResponse>> SaveCredential(
        Guid? id, SaveDnsCredentialProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveCredentialProfileAsync(new(id, request.Name, request.AuthenticationMode,
            request.UserName, request.Password, request.IsEnabled, DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.IsSuccess && result.Value is not null ? Ok(Map(result.Value)) : BadRequest(new { message = result.Message });
    }

    private async Task<ActionResult<DnsServerResponse>> SaveServer(
        Guid? id, SaveDnsServerRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveServerAsync(new(id, request.DisplayName, request.HostName, request.Port,
            request.Environment, request.CredentialProfileId, request.IsEnabled, request.SyncIntervalMinutes,
            request.TlsCertificateThumbprint, request.Notes, DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.IsSuccess && result.Value is not null ? Ok(Map(result.Value)) : BadRequest(new { message = result.Message });
    }

    private static DnsManagementSettingsResponse Map(AppModels.DnsManagementSettingsModel x) => new(
        x.IsEnabled, x.AutomaticSyncEnabled, x.DefaultSyncIntervalMinutes, x.HealthCheckIntervalMinutes,
        x.CommandTimeoutSeconds, x.MaxParallelServers, x.SnapshotRetentionDays, x.SyncRecordInventory,
        x.PromptForFullSyncOnComparisonOpen, x.ComparisonSnapshotStaleAfterMinutes, x.UpdatedAt, x.UpdatedBy);
    private static DnsCredentialProfileResponse Map(AppModels.DnsCredentialProfileModel x) => new(
        x.Id, x.Name, x.AuthenticationMode, x.UserName, x.HasPassword, x.IsEnabled,
        x.LastValidatedAt, x.LastValidationStatus, x.LastValidationMessage);
    private static DnsServerResponse Map(AppModels.DnsServerModel x) => new(
        x.Id, x.DisplayName, x.HostName, x.Port, x.Environment, x.CredentialProfileId,
        x.CredentialProfileName, x.IsEnabled, x.SyncIntervalMinutes, x.TlsCertificateThumbprint,
        x.Notes, x.OperatingSystemVersion, x.DnsServerVersion, x.LastSeenAt,
        x.LastSuccessfulSyncAt, x.LastSyncStatus, x.LastSyncMessage);
    private static DnsServerConnectionTestResponse Map(AppModels.DnsServerConnectionTestModel x) => new(
        x.ServerId, x.ServerDisplayName, x.Success, x.FailureKind, x.Message, x.HostAgentAvailable,
        x.NetworkReachable, x.TlsValidated, x.AuthenticationSucceeded, x.DnsModuleAvailable,
        x.DnsServiceReachable, x.OperatingSystemVersion, x.PowerShellVersion, x.DnsModuleVersion,
        x.DnsServerVersion, x.ZoneCount, x.Capabilities is null ? null : new(
            x.Capabilities.Zones, x.Capabilities.Records, x.Capabilities.ServerSettings,
            x.Capabilities.Dnssec, x.Capabilities.Policies, x.Capabilities.Scopes, x.Capabilities.Cache),
        x.TestedAt);
    private static DnsSyncJobResponse Map(AppModels.DnsSyncJobModel x) => new(
        x.Id, x.BatchId, x.ServerId, x.ServerDisplayName, x.Scope, x.Trigger, x.Status,
        x.AttemptCount, x.RequestedAt, x.StartedAt, x.CompletedAt, x.ErrorCode, x.Message, x.AlreadyQueued);
    private static DnsServerSettingsResponse Map(AppModels.DnsServerSettingsModel x) => new(
        x.ForwarderAddresses, x.ForwarderUseRootHint, x.ForwarderTimeoutSeconds,
        x.ForwarderEnableReordering, x.RecursionEnabled, x.RecursionAdditionalTimeoutSeconds,
        x.RecursionRetryIntervalSeconds, x.RecursionTimeoutSeconds,
        x.RecursionSecureResponse, x.StateToken);
    private static DnsServerOperationResponse Map(AppModels.DnsServerSettingsOperationModel x) => new(
        x.Success, x.ErrorCode, x.Message, x.Settings is null ? null : Map(x.Settings));
}
