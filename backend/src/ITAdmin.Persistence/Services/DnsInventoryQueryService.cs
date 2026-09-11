using System.Text.Json;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Persistence.Services;

public sealed class DnsInventoryQueryService(AppDbContext context) : IDnsInventoryQueryService
{
    public async Task<IReadOnlyList<DnsInventoryServerModel>> GetServersAsync(
        CancellationToken cancellationToken = default)
    {
        var staleAfterMinutes = await context.DnsManagementSettings.AsNoTracking()
            .Select(x => (int?)x.ComparisonSnapshotStaleAfterMinutes)
            .SingleOrDefaultAsync(cancellationToken) ?? 15;
        var now = DateTime.UtcNow;
        var servers = await context.DnsServers.AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.Environment,
                x.IsEnabled,
                x.LastSyncStatus,
                x.LastSyncMessage,
            })
            .ToListAsync(cancellationToken);
        var serverIds = servers.Select(x => x.Id).ToArray();
        var snapshots = await context.DnsInventorySnapshots.AsNoTracking()
            .Where(x => x.IsActive && serverIds.Contains(x.DnsServerId))
            .Select(x => new
            {
                x.Id,
                x.Version,
                x.DnsServerId,
                x.Scope,
                x.CompletedAt,
                x.ZoneCount,
                x.RecordCount,
            })
            .ToDictionaryAsync(x => x.DnsServerId, cancellationToken);

        return servers.Select(server =>
        {
            snapshots.TryGetValue(server.Id, out var snapshot);
            var completedAt = snapshot?.CompletedAt;
            return new DnsInventoryServerModel(
                server.Id, server.DisplayName, server.Environment, server.IsEnabled,
                snapshot?.Id, snapshot?.Version, snapshot?.Scope, completedAt,
                snapshot?.ZoneCount ?? 0, snapshot?.RecordCount ?? 0,
                server.LastSyncStatus, server.LastSyncMessage,
                completedAt is null || completedAt < now.AddMinutes(-staleAfterMinutes),
                snapshot is not null);
        }).ToList();
    }

    public async Task<PagedResult<DnsZoneInventoryModel>> GetZonesAsync(
        DnsZoneInventoryQuery query, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Clamp(query.PageNumber, 1, 1_000_000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var search = Normalize(query.Search, 253)?.ToLowerInvariant();
        var source = context.DnsZoneSnapshots.AsNoTracking()
            .Where(x => x.InventorySnapshot.IsActive);
        if (query.ServerId is { } serverId)
            source = source.Where(x => x.InventorySnapshot.DnsServerId == serverId);
        if (search is not null)
            source = source.Where(x => x.Name.ToLower().Contains(search)
                || x.ZoneType.ToLower().Contains(search)
                || x.InventorySnapshot.DnsServer.DisplayName.ToLower().Contains(search));

        var totalCount = await source.CountAsync(cancellationToken);
        var rows = await ProjectZones(source)
            .OrderBy(x => x.ServerDisplayName).ThenBy(x => x.Name).ThenBy(x => x.VirtualizationInstance)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return Page(rows.Select(MapZone).ToList(), pageNumber, pageSize, totalCount);
    }

    public async Task<DnsZoneInventoryModel?> GetZoneAsync(
        Guid zoneSnapshotId, CancellationToken cancellationToken = default)
    {
        var row = await ProjectZones(context.DnsZoneSnapshots.AsNoTracking()
                .Where(x => x.Id == zoneSnapshotId && x.InventorySnapshot.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : MapZone(row);
    }

    public async Task<PagedResult<DnsRecordInventoryModel>> GetRecordsAsync(
        DnsRecordInventoryQuery query, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Clamp(query.PageNumber, 1, 1_000_000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var search = Normalize(query.Search, 512)?.ToLowerInvariant();
        var recordType = Normalize(query.RecordType, 32)?.ToUpperInvariant();
        var source = context.DnsRecordSnapshots.AsNoTracking()
            .Where(x => x.DnsZoneSnapshotId == query.ZoneSnapshotId
                && x.ZoneSnapshot.InventorySnapshot.IsActive);
        if (search is not null)
            source = source.Where(x => x.RelativeName.ToLower().Contains(search)
                || x.FullyQualifiedName.ToLower().Contains(search)
                || x.CanonicalValue.ToLower().Contains(search));
        if (recordType is not null)
            source = source.Where(x => x.RecordType == recordType);

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderBy(x => x.RelativeName).ThenBy(x => x.RecordType)
            .ThenBy(x => x.ZoneScope).ThenBy(x => x.CanonicalValue)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new DnsRecordInventoryModel(
                x.Id, x.RelativeName, x.FullyQualifiedName, x.RecordType,
                x.CanonicalValue, x.RecordDataJson, x.TimeToLiveSeconds,
                x.Timestamp, x.ZoneScope, x.VirtualizationInstance, x.RecordHash))
            .ToListAsync(cancellationToken);
        return Page(items, pageNumber, pageSize, totalCount);
    }

    private static IQueryable<ZoneRow> ProjectZones(IQueryable<Domain.Entities.DnsZoneSnapshot> source) =>
        source.Select(x => new ZoneRow(
            x.Id, x.DnsInventorySnapshotId, x.InventorySnapshot.DnsServerId,
            x.InventorySnapshot.DnsServer.DisplayName, x.InventorySnapshot.DnsServer.Environment,
            x.Name, x.ZoneType, x.IsReverseLookupZone, x.IsDsIntegrated, x.IsSigned, x.IsPaused,
            x.DynamicUpdate, x.ReplicationScope, x.DirectoryPartitionName, x.ZoneFile,
            x.VirtualizationInstance, x.PropertiesJson, x.Records.Count,
            x.InventorySnapshot.CompletedAt ?? x.InventorySnapshot.StartedAt));

    private static DnsZoneInventoryModel MapZone(ZoneRow x) => new(
        x.Id, x.SnapshotId, x.ServerId, x.ServerDisplayName, x.Environment,
        x.Name, x.ZoneType, x.IsReverseLookupZone, x.IsDsIntegrated, x.IsSigned, x.IsPaused,
        x.DynamicUpdate, x.ReplicationScope, x.DirectoryPartitionName, x.ZoneFile,
        x.VirtualizationInstance, ReadZoneScopes(x.PropertiesJson), x.RecordCount, x.SnapshotCompletedAt);

    private static IReadOnlyList<string> ReadZoneScopes(string? propertiesJson)
    {
        if (string.IsNullOrWhiteSpace(propertiesJson)) return [];
        try
        {
            using var document = JsonDocument.Parse(propertiesJson);
            if (!document.RootElement.TryGetProperty("zoneScopes", out var scopes)
                || scopes.ValueKind != JsonValueKind.Array) return [];
            return scopes.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }

    private static PagedResult<T> Page<T>(IReadOnlyCollection<T> items, int pageNumber, int pageSize, int totalCount) =>
        new(items, pageNumber, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));

    private sealed record ZoneRow(
        Guid Id, Guid SnapshotId, Guid ServerId, string ServerDisplayName,
        DnsServerEnvironment Environment, string Name, string ZoneType,
        bool IsReverseLookupZone, bool IsDsIntegrated, bool IsSigned, bool IsPaused,
        string? DynamicUpdate, string? ReplicationScope, string? DirectoryPartitionName,
        string? ZoneFile, string? VirtualizationInstance, string? PropertiesJson,
        int RecordCount, DateTime SnapshotCompletedAt);
}
