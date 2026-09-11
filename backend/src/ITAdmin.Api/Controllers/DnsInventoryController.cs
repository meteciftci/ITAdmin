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
public sealed class DnsInventoryController(IDnsInventoryQueryService service) : ControllerBase
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
}
