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
    IDnsInventorySyncService inventorySyncService) : ControllerBase
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
        x.VirtualizationInstance, x.ZoneScopes, x.RecordCount, x.SnapshotCompletedAt);

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
}
