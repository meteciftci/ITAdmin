using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.Common;
using ITAdmin.Api.Contracts.DnsManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppModels = ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/dns-management/inventory")]
[Authorize]
public sealed class DnsInventoryController(
    IDnsInventoryQueryService service,
    IDnsInventorySyncService inventorySyncService,
    IDnsRecordMutationService recordMutationService,
    IDnsZoneMutationService zoneMutationService) : ControllerBase
{
    [HttpGet("servers")]
    [RequireAnyPermission(DnsManagementPermissions.ZonesView, DnsManagementPermissions.RecordsView)]
    public async Task<ActionResult<IReadOnlyList<DnsInventoryServerResponse>>> GetServers(
        CancellationToken cancellationToken) =>
        Ok((await service.GetServersAsync(cancellationToken)).Select(MapServer).ToList());

    [HttpGet("zones")]
    [RequireAnyPermission(DnsManagementPermissions.ZonesView, DnsManagementPermissions.RecordsView)]
    public async Task<ActionResult<PagedResponse<DnsZoneInventoryResponse>>> GetZones(
        [FromQuery] Guid? serverId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetZonesAsync(
            new AppModels.DnsZoneInventoryQuery(serverId, search, pageNumber, pageSize), cancellationToken);
        return Ok(new PagedResponse<DnsZoneInventoryResponse>(
            result.Items.Select(MapZone).ToList(), result.PageNumber, result.PageSize,
            result.TotalCount, result.TotalPages));
    }

    [HttpGet("zones/{id:guid}")]
    [RequireAnyPermission(DnsManagementPermissions.ZonesView, DnsManagementPermissions.RecordsView)]
    public async Task<ActionResult<DnsZoneInventoryResponse>> GetZone(
        Guid id, CancellationToken cancellationToken)
    {
        var zone = await service.GetZoneAsync(id, cancellationToken);
        return zone is null
            ? NotFound(new { message = "The active DNS zone snapshot was not found." })
            : Ok(MapZone(zone));
    }

    [HttpPost("servers/{serverId:guid}/zones")]
    [RequirePermission(DnsManagementPermissions.ZonesCreate)]
    public Task<ActionResult<DnsZoneMutationResponse>> CreateZone(
        Guid serverId, CreateDnsZoneRequest request, CancellationToken cancellationToken) =>
        MutateZone(new(serverId, null, request.Name, request.ZoneKind, request.IsDsIntegrated,
            request.DynamicUpdate, request.ReplicationScope, request.DirectoryPartitionName,
            request.ZoneFile, request.MasterServers ?? [], request.ForwarderTimeoutSeconds,
            request.UseRecursion, AppModels.DnsZoneMutationKind.Create,
            DnsManagementActorResolver.Resolve(this)), cancellationToken);

    [HttpPut("zones/{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ZonesUpdate)]
    public Task<ActionResult<DnsZoneMutationResponse>> UpdateZone(
        Guid id, UpdateDnsZoneRequest request, CancellationToken cancellationToken) =>
        MutateZone(new(Guid.Empty, id, string.Empty, AppModels.DnsZoneKind.Primary, false,
            request.DynamicUpdate, null, null, null, request.MasterServers ?? [],
            request.ForwarderTimeoutSeconds, request.UseRecursion,
            AppModels.DnsZoneMutationKind.Update, DnsManagementActorResolver.Resolve(this)), cancellationToken);

    [HttpDelete("zones/{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ZonesDelete)]
    public Task<ActionResult<DnsZoneMutationResponse>> DeleteZone(
        Guid id, CancellationToken cancellationToken) =>
        MutateZone(new(Guid.Empty, id, string.Empty, AppModels.DnsZoneKind.Primary, false,
            null, null, null, null, [], null, null,
            AppModels.DnsZoneMutationKind.Delete, DnsManagementActorResolver.Resolve(this)), cancellationToken);

    [HttpGet("zones/{id:guid}/records")]
    [RequirePermission(DnsManagementPermissions.RecordsView)]
    public async Task<ActionResult<PagedResponse<DnsRecordInventoryResponse>>> GetRecords(
        Guid id,
        [FromQuery] string? search,
        [FromQuery] string? recordType,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetRecordsAsync(
            new AppModels.DnsRecordInventoryQuery(id, search, recordType, pageNumber, pageSize), cancellationToken);
        return Ok(new PagedResponse<DnsRecordInventoryResponse>(
            result.Items.Select(MapRecord).ToList(), result.PageNumber, result.PageSize,
            result.TotalCount, result.TotalPages));
    }

    [HttpPost("zones/{id:guid}/records")]
    [RequirePermission(DnsManagementPermissions.RecordsCreate)]
    public Task<ActionResult<DnsRecordMutationResponse>> CreateRecord(
        Guid id, CreateDnsRecordRequest request, CancellationToken cancellationToken) =>
        MutateRecord(new(id, null, request.RelativeName, request.RecordType, request.Values,
            request.TimeToLiveSeconds, request.ZoneScope, null,
            AppModels.DnsRecordMutationKind.Create, DnsManagementActorResolver.Resolve(this)), cancellationToken);

    [HttpPut("zones/{zoneId:guid}/records/{recordId:guid}")]
    [RequirePermission(DnsManagementPermissions.RecordsUpdate)]
    public Task<ActionResult<DnsRecordMutationResponse>> UpdateRecord(
        Guid zoneId, Guid recordId, UpdateDnsRecordRequest request, CancellationToken cancellationToken)
        => MutateRecord(new(zoneId, recordId, string.Empty, string.Empty,
            request.Values, request.TimeToLiveSeconds, null, request.ExpectedRecordHash,
            AppModels.DnsRecordMutationKind.Update, DnsManagementActorResolver.Resolve(this)), cancellationToken);

    [HttpDelete("zones/{zoneId:guid}/records/{recordId:guid}")]
    [RequirePermission(DnsManagementPermissions.RecordsDelete)]
    public Task<ActionResult<DnsRecordMutationResponse>> DeleteRecord(
        Guid zoneId, Guid recordId, [FromBody] DeleteDnsRecordRequest request,
        CancellationToken cancellationToken)
        => MutateRecord(new(zoneId, recordId, string.Empty, string.Empty,
            [], 0, null, request.ExpectedRecordHash,
            AppModels.DnsRecordMutationKind.Delete, DnsManagementActorResolver.Resolve(this)), cancellationToken);

    [HttpGet("comparison/context")]
    [RequirePermission(DnsManagementPermissions.Compare)]
    public async Task<ActionResult<DnsComparisonContextResponse>> GetComparisonContext(
        CancellationToken cancellationToken)
    {
        var result = await service.GetComparisonContextAsync(cancellationToken);
        return Ok(new DnsComparisonContextResponse(
            result.PromptForFullSyncOnOpen, result.LastFullInventorySyncAt,
            result.SynchronizationInProgress, result.EnabledServerCount,
            result.UnavailableServerCount, result.Servers.Select(MapServer).ToList()));
    }

    [HttpGet("comparison/zones")]
    [RequirePermission(DnsManagementPermissions.Compare)]
    public async Task<ActionResult<IReadOnlyList<DnsComparisonZoneResponse>>> GetComparisonZones(
        [FromQuery] Guid[]? serverIds,
        [FromQuery] string? search,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var ids = serverIds?.Distinct().ToArray() ?? [];
        if (ids.Length is < 1 or > 10)
            return BadRequest(new { message = "Select between one and ten DNS servers." });
        var result = await service.GetComparisonZonesAsync(ids, search, limit, cancellationToken);
        return Ok(result.Select(x => new DnsComparisonZoneResponse(x.Name, x.ServerCount)).ToList());
    }

    [HttpPost("comparison/query")]
    [RequirePermission(DnsManagementPermissions.Compare)]
    public async Task<ActionResult<DnsComparisonResponse>> Compare(
        DnsComparisonRequest request, CancellationToken cancellationToken)
    {
        var serverIds = request.ServerIds?.Distinct().ToArray() ?? [];
        var zoneNames = request.ZoneNames?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (serverIds.Length is < 2 or > 10)
            return BadRequest(new { message = "Select between two and ten DNS servers." });
        if (zoneNames.Length is < 1 or > 20)
            return BadRequest(new { message = "Select between one and twenty DNS zones." });

        var result = await service.CompareAsync(new(
            serverIds, zoneNames, request.CompareTimeToLive, request.Search,
            request.PageNumber, request.PageSize), cancellationToken);
        return Ok(new DnsComparisonResponse(
            result.Servers.Select(MapServer).ToList(),
            result.Items.Select(MapComparisonRow).ToList(),
            result.PageNumber, result.PageSize, result.TotalCount, result.TotalPages));
    }

    [HttpPost("comparison/synchronizations")]
    [RequirePermission(DnsManagementPermissions.Synchronize)]
    public async Task<ActionResult<DnsSyncBatchResponse>> SynchronizeAll(
        CancellationToken cancellationToken)
    {
        var result = await inventorySyncService.EnqueueAllEnabledAsync(
            DnsManagementActorResolver.Resolve(this), cancellationToken);
        return Accepted(new DnsSyncBatchResponse(
            result.BatchId, result.TargetedCount, result.QueuedCount,
            result.AlreadyQueuedCount, result.FailedCount,
            result.Jobs.Select(MapJob).ToList()));
    }

    private static DnsInventoryServerResponse MapServer(AppModels.DnsInventoryServerModel x) => new(
        x.ServerId, x.ServerDisplayName, x.Environment, x.IsEnabled,
        x.SnapshotId, x.SnapshotVersion, x.SnapshotScope, x.SnapshotCompletedAt,
        x.ZoneCount, x.RecordCount, x.LastSyncStatus, x.LastSyncMessage, x.IsStale, x.IsAvailable);

    private static DnsZoneInventoryResponse MapZone(AppModels.DnsZoneInventoryModel x) => new(
        x.Id, x.SnapshotId, x.ServerId, x.ServerDisplayName, x.Environment,
        x.Name, x.ZoneType, x.IsReverseLookupZone, x.IsDsIntegrated, x.IsSigned, x.IsPaused,
        x.DynamicUpdate, x.ReplicationScope, x.DirectoryPartitionName, x.ZoneFile,
        x.VirtualizationInstance, x.ZoneScopes, x.IsAutoCreated, x.MasterServers,
        x.ForwarderTimeoutSeconds, x.UseRecursion, x.RecordCount, x.SnapshotCompletedAt);

    private static DnsRecordInventoryResponse MapRecord(AppModels.DnsRecordInventoryModel x) => new(
        x.Id, x.RelativeName, x.FullyQualifiedName, x.RecordType, x.CanonicalValue,
        x.RecordDataJson, x.TimeToLiveSeconds, x.Timestamp, x.ZoneScope,
        x.VirtualizationInstance, x.RecordHash);

    private static DnsComparisonRowResponse MapComparisonRow(AppModels.DnsComparisonRowModel x) => new(
        x.ZoneName, x.RelativeName, x.RecordType, x.ZoneScope, x.VirtualizationInstance,
        x.Cells.Select(cell => new DnsComparisonCellResponse(
            cell.ServerId, cell.Status, cell.Values, cell.TimeToLiveValues)).ToList());

    private static DnsSyncJobResponse MapJob(AppModels.DnsSyncJobModel x) => new(
        x.Id, x.BatchId, x.ServerId, x.ServerDisplayName, x.Scope, x.Trigger, x.Status,
        x.AttemptCount, x.RequestedAt, x.StartedAt, x.CompletedAt,
            x.ErrorCode, x.Message, x.AlreadyQueued);

    private async Task<ActionResult<DnsRecordMutationResponse>> MutateRecord(
        AppModels.DnsRecordMutationCommand command, CancellationToken cancellationToken)
    {
        var result = await recordMutationService.ExecuteAsync(command, cancellationToken);
        var response = new DnsRecordMutationResponse(
            result.Success, result.ErrorCode, result.Message,
            result.Before is null ? null : MapRecord(result.Before),
            result.After is null ? null : MapRecord(result.After),
            result.Synchronization is null ? null : MapJob(result.Synchronization));
        if (result.Success) return Ok(response);
        return result.ErrorCode is "RecordChanged" or "SnapshotExpired"
            ? Conflict(response)
            : BadRequest(response);
    }

    private async Task<ActionResult<DnsZoneMutationResponse>> MutateZone(
        AppModels.DnsZoneMutationCommand command, CancellationToken cancellationToken)
    {
        var result = await zoneMutationService.ExecuteAsync(command, cancellationToken);
        var response = new DnsZoneMutationResponse(
            result.Success, result.ErrorCode, result.Message,
            result.Before is null ? null : MapZone(result.Before),
            result.After is null ? null : MapZone(result.After),
            result.Synchronization is null ? null : MapJob(result.Synchronization));
        if (result.Success) return Ok(response);
        return result.ErrorCode is "ZoneChanged" or "SnapshotExpired"
            ? Conflict(response)
            : BadRequest(response);
    }
}
