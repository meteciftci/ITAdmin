using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsInventoryQueryService
{
    Task<IReadOnlyList<DnsInventoryServerModel>> GetServersAsync(
        CancellationToken cancellationToken = default);
    Task<PagedResult<DnsZoneInventoryModel>> GetZonesAsync(
        DnsZoneInventoryQuery query, CancellationToken cancellationToken = default);
    Task<DnsZoneInventoryModel?> GetZoneAsync(
        Guid zoneSnapshotId, CancellationToken cancellationToken = default);
    Task<PagedResult<DnsRecordInventoryModel>> GetRecordsAsync(
        DnsRecordInventoryQuery query, CancellationToken cancellationToken = default);
}
