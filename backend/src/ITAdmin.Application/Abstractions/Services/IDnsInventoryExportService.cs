using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsInventoryExportService
{
    Task<DnsExportResultModel> ExportZonesAsync(
        Guid? serverId, string? search, DnsActorContext actor,
        CancellationToken cancellationToken = default);
    Task<DnsExportResultModel> ExportRecordsAsync(
        Guid zoneSnapshotId, string? search, string? recordType, DnsActorContext actor,
        CancellationToken cancellationToken = default);
    Task<DnsExportResultModel> ExportComparisonAsync(
        DnsComparisonQuery query, DnsActorContext actor,
        CancellationToken cancellationToken = default);
}
