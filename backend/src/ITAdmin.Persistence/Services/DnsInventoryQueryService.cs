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

    public async Task<DnsComparisonContextModel> GetComparisonContextAsync(
        CancellationToken cancellationToken = default)
    {
        var prompt = await context.DnsManagementSettings.AsNoTracking()
            .Select(x => (bool?)x.PromptForFullSyncOnComparisonOpen)
            .SingleOrDefaultAsync(cancellationToken) ?? true;
        var servers = await GetServersAsync(cancellationToken);
        var enabled = servers.Where(x => x.IsEnabled).ToList();
        var fullSnapshots = enabled.Where(IsFullInventoryAvailable).ToList();
        var lastFullSync = enabled.Count > 0 && fullSnapshots.Count == enabled.Count
            ? fullSnapshots.Min(x => x.SnapshotCompletedAt)
            : null;
        return new DnsComparisonContextModel(
            prompt,
            lastFullSync,
            enabled.Any(x => x.LastSyncStatus is "Pending" or "Running"),
            enabled.Count,
            enabled.Count(x => !IsFullInventoryAvailable(x)),
            servers);
    }

    public async Task<IReadOnlyList<DnsComparisonZoneModel>> GetComparisonZonesAsync(
        IReadOnlyList<Guid> serverIds, string? search, int limit,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeServerIds(serverIds);
        if (ids.Length == 0) return [];
        var normalizedSearch = Normalize(search, 253)?.ToLowerInvariant();
        var source = context.DnsZoneSnapshots.AsNoTracking()
            .Where(x => x.InventorySnapshot.IsActive
                && ids.Contains(x.InventorySnapshot.DnsServerId));
        if (normalizedSearch is not null)
            source = source.Where(x => x.Name.ToLower().Contains(normalizedSearch));

        return await source
            .GroupBy(x => x.Name.ToLower())
            .Select(x => new DnsComparisonZoneModel(
                x.Key, x.Select(y => y.InventorySnapshot.DnsServerId).Distinct().Count()))
            .OrderBy(x => x.Name)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<DnsComparisonResultModel> CompareAsync(
        DnsComparisonQuery query, CancellationToken cancellationToken = default)
    {
        var serverIds = NormalizeServerIds(query.ServerIds);
        var zoneNames = NormalizeZoneNames(query.ZoneNames);
        var pageNumber = Math.Clamp(query.PageNumber, 1, 1_000_000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        if (serverIds.Length == 0 || zoneNames.Length == 0)
            return new([], [], pageNumber, pageSize, 0, 0);

        var serverSet = serverIds.ToHashSet();
        var servers = (await GetServersAsync(cancellationToken))
            .Where(x => serverSet.Contains(x.ServerId)).ToList();
        var search = Normalize(query.Search, 512)?.ToLowerInvariant();
        var source = context.DnsRecordSnapshots.AsNoTracking()
            .Where(x => x.ZoneSnapshot.InventorySnapshot.IsActive
                && serverIds.Contains(x.ZoneSnapshot.InventorySnapshot.DnsServerId)
                && zoneNames.Contains(x.ZoneSnapshot.Name.ToLower()));
        if (search is not null)
            source = source.Where(x => x.RelativeName.ToLower().Contains(search)
                || x.FullyQualifiedName.ToLower().Contains(search)
                || x.RecordType.ToLower().Contains(search)
                || x.CanonicalValue.ToLower().Contains(search));

        var keys = source.Select(x => new ComparisonKey(
                x.ZoneSnapshot.Name.ToLower(), x.RelativeName.ToLower(), x.RecordType.ToUpper(),
                x.ZoneScope == null ? null : x.ZoneScope.ToLower(),
                x.VirtualizationInstance == null ? null : x.VirtualizationInstance.ToLower()))
            .Distinct();
        var totalCount = await keys.CountAsync(cancellationToken);
        var pageKeys = await keys
            .OrderBy(x => x.ZoneName).ThenBy(x => x.RelativeName).ThenBy(x => x.RecordType)
            .ThenBy(x => x.ZoneScope).ThenBy(x => x.VirtualizationInstance)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        if (pageKeys.Count == 0)
            return new(servers, [], pageNumber, pageSize, totalCount,
                TotalPages(totalCount, pageSize));

        var pageZones = pageKeys.Select(x => x.ZoneName).Distinct().ToArray();
        var pageOwners = pageKeys.Select(x => x.RelativeName).Distinct().ToArray();
        var pageTypes = pageKeys.Select(x => x.RecordType).Distinct().ToArray();
        var candidates = await source
            .Where(x => pageZones.Contains(x.ZoneSnapshot.Name.ToLower())
                && pageOwners.Contains(x.RelativeName.ToLower())
                && pageTypes.Contains(x.RecordType.ToUpper()))
            .Select(x => new ComparisonRecord(
                x.ZoneSnapshot.InventorySnapshot.DnsServerId,
                x.ZoneSnapshot.Name.ToLower(), x.RelativeName.ToLower(), x.RecordType.ToUpper(),
                x.ZoneScope == null ? null : x.ZoneScope.ToLower(),
                x.VirtualizationInstance == null ? null : x.VirtualizationInstance.ToLower(),
                x.CanonicalValue, x.TimeToLiveSeconds))
            .ToListAsync(cancellationToken);
        var pageKeySet = pageKeys.ToHashSet();
        var records = candidates.Where(x => pageKeySet.Contains(x.Key)).ToList();
        var rows = pageKeys.Select(key => BuildComparisonRow(
            key, servers, records, query.CompareTimeToLive)).ToList();

        return new(servers, rows, pageNumber, pageSize, totalCount,
            TotalPages(totalCount, pageSize));
    }

    private static IQueryable<ZoneRow> ProjectZones(IQueryable<Domain.Entities.DnsZoneSnapshot> source) =>
        source.Select(x => new ZoneRow(
            x.Id, x.DnsInventorySnapshotId, x.InventorySnapshot.DnsServerId,
            x.InventorySnapshot.DnsServer.DisplayName, x.InventorySnapshot.DnsServer.Environment,
            x.Name, x.ZoneType, x.IsReverseLookupZone, x.IsDsIntegrated, x.IsSigned, x.IsPaused,
            x.DynamicUpdate, x.ReplicationScope, x.DirectoryPartitionName, x.ZoneFile,
            x.VirtualizationInstance, x.PropertiesJson, x.Records.Count,
            x.InventorySnapshot.CompletedAt ?? x.InventorySnapshot.StartedAt));

    private static DnsZoneInventoryModel MapZone(ZoneRow x)
    {
        var properties = ReadZoneProperties(x.PropertiesJson);
        return new(
            x.Id, x.SnapshotId, x.ServerId, x.ServerDisplayName, x.Environment,
            x.Name, x.ZoneType, x.IsReverseLookupZone, x.IsDsIntegrated, x.IsSigned, x.IsPaused,
            x.DynamicUpdate, x.ReplicationScope, x.DirectoryPartitionName, x.ZoneFile,
            x.VirtualizationInstance, properties.ZoneScopes, properties.IsAutoCreated,
            properties.MasterServers, properties.ForwarderTimeoutSeconds, properties.UseRecursion,
            x.RecordCount, x.SnapshotCompletedAt);
    }

    private static ZoneProperties ReadZoneProperties(string? propertiesJson)
    {
        if (string.IsNullOrWhiteSpace(propertiesJson)) return new([], false, [], null, null);
        try
        {
            using var document = JsonDocument.Parse(propertiesJson);
            var root = document.RootElement;
            var scopes = root.TryGetProperty("zoneScopes", out var scopeElement)
                && scopeElement.ValueKind == JsonValueKind.Array
                ? scopeElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : [];
            var masters = root.TryGetProperty("masterServers", out var masterElement)
                && masterElement.ValueKind == JsonValueKind.Array
                ? masterElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : [];
            var autoCreated = root.TryGetProperty("isAutoCreated", out var autoElement)
                && autoElement.ValueKind is JsonValueKind.True or JsonValueKind.False && autoElement.GetBoolean();
            var timeout = root.TryGetProperty("forwarderTimeoutSeconds", out var timeoutElement)
                && timeoutElement.TryGetInt32(out var timeoutValue) ? timeoutValue : (int?)null;
            var recursion = root.TryGetProperty("useRecursion", out var recursionElement)
                && recursionElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? recursionElement.GetBoolean() : (bool?)null;
            return new(scopes, autoCreated, masters, timeout, recursion);
        }
        catch (JsonException)
        {
            return new([], false, [], null, null);
        }
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }

    private static PagedResult<T> Page<T>(IReadOnlyCollection<T> items, int pageNumber, int pageSize, int totalCount) =>
        new(items, pageNumber, pageSize, totalCount,
            TotalPages(totalCount, pageSize));

    private static int TotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

    private static Guid[] NormalizeServerIds(IReadOnlyList<Guid> serverIds) =>
        serverIds.Where(x => x != Guid.Empty).Distinct().Take(10).ToArray();

    private static string[] NormalizeZoneNames(IReadOnlyList<string> zoneNames) =>
        zoneNames.Select(x => Normalize(x, 253)?.ToLowerInvariant())
            .Where(x => x is not null).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();

    private static bool IsFullInventoryAvailable(DnsInventoryServerModel server) =>
        server.IsAvailable && server.SnapshotScope == DnsSyncScope.FullInventory;

    private static DnsComparisonRowModel BuildComparisonRow(
        ComparisonKey key, IReadOnlyList<DnsInventoryServerModel> servers,
        IReadOnlyList<ComparisonRecord> records, bool compareTimeToLive)
    {
        var byServer = records.Where(x => x.Key == key).GroupBy(x => x.ServerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var signatures = new Dictionary<Guid, string>();
        foreach (var server in servers.Where(IsFullInventoryAvailable))
        {
            byServer.TryGetValue(server.ServerId, out var serverRecords);
            signatures[server.ServerId] = serverRecords is null
                ? "<missing>"
                : string.Join('\u001e', serverRecords
                    .Select(x => compareTimeToLive ? $"{x.CanonicalValue}\u001f{x.TimeToLiveSeconds}" : x.CanonicalValue)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        }
        var isDifferent = signatures.Values.Distinct(StringComparer.Ordinal).Skip(1).Any();
        var cells = servers.Select(server =>
        {
            byServer.TryGetValue(server.ServerId, out var serverRecords);
            var values = serverRecords?.Select(x => x.CanonicalValue)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];
            var ttls = serverRecords?.Select(x => x.TimeToLiveSeconds).Distinct().Order().ToArray() ?? [];
            var status = !IsFullInventoryAvailable(server) ? "Unavailable"
                : server.IsStale ? "Stale"
                : values.Length == 0 ? "Missing"
                : isDifferent ? "Different" : "Equal";
            return new DnsComparisonCellModel(server.ServerId, status, values, ttls);
        }).ToList();
        return new(key.ZoneName, key.RelativeName, key.RecordType,
            key.ZoneScope, key.VirtualizationInstance, cells);
    }

    private sealed record ZoneRow(
        Guid Id, Guid SnapshotId, Guid ServerId, string ServerDisplayName,
        DnsServerEnvironment Environment, string Name, string ZoneType,
        bool IsReverseLookupZone, bool IsDsIntegrated, bool IsSigned, bool IsPaused,
        string? DynamicUpdate, string? ReplicationScope, string? DirectoryPartitionName,
        string? ZoneFile, string? VirtualizationInstance, string? PropertiesJson,
        int RecordCount, DateTime SnapshotCompletedAt);

    private sealed record ZoneProperties(
        IReadOnlyList<string> ZoneScopes, bool IsAutoCreated,
        IReadOnlyList<string> MasterServers, int? ForwarderTimeoutSeconds, bool? UseRecursion);

    private sealed record ComparisonKey(
        string ZoneName, string RelativeName, string RecordType,
        string? ZoneScope, string? VirtualizationInstance);

    private sealed record ComparisonRecord(
        Guid ServerId, string ZoneName, string RelativeName, string RecordType,
        string? ZoneScope, string? VirtualizationInstance,
        string CanonicalValue, int TimeToLiveSeconds)
    {
        public ComparisonKey Key => new(
            ZoneName, RelativeName, RecordType, ZoneScope, VirtualizationInstance);
    }
}
