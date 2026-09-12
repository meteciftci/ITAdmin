using System.Data;
using System.Text;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ITAdmin.Api.Services;

public sealed class DnsInventoryExportService(
    IDnsInventoryQueryService inventory,
    AppDbContext context) : IDnsInventoryExportService
{
    private const int PageSize = 100;
    private const int MaxZoneRows = 10_000;
    private const int MaxRecordRows = 50_000;
    private const int MaxComparisonRows = 25_000;
    private const int MaxExportCharacters = 10_000_000;
    private const string CsvContentType = "text/csv; charset=utf-8";

    public async Task<DnsExportResultModel> ExportZonesAsync(
        Guid? serverId, string? search, DnsActorContext actor,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginSnapshotAsync(cancellationToken);
        var first = await inventory.GetZonesAsync(
            new(serverId, search, 1, PageSize), cancellationToken);
        if (first.TotalCount > MaxZoneRows)
            return TooLarge(MaxZoneRows);
        var csv = new CsvDocument(MaxExportCharacters);
        csv.Add(new[] { "Server", "Environment", "Zone", "Type", "Reverse", "AD Integrated", "Signed", "Paused", "Dynamic Update", "Replication Scope", "Virtualization Instance", "Zone Scopes", "Record Count", "Snapshot Time (UTC)" });
        var complete = await AppendPagesAsync(first, page => inventory.GetZonesAsync(
                new(serverId, search, page, PageSize), cancellationToken), zone =>
            new[]
            {
                zone.ServerDisplayName, zone.Environment.ToString(), zone.Name, zone.ZoneType,
                Bool(zone.IsReverseLookupZone), Bool(zone.IsDsIntegrated), Bool(zone.IsSigned),
                Bool(zone.IsPaused), zone.DynamicUpdate, zone.ReplicationScope,
                zone.VirtualizationInstance, string.Join(" | ", zone.ZoneScopes),
                zone.RecordCount.ToString(), Utc(zone.SnapshotCompletedAt),
            }, csv);
        if (!complete) return TooLargeFile();
        var file = File(csv, "dns-zones");
        await AuditAsync("DnsZonesExport", $"Exported {first.TotalCount} DNS zone inventory rows.", actor, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new(true, null, "DNS zone inventory exported.", file);
    }

    public async Task<DnsExportResultModel> ExportRecordsAsync(
        Guid zoneSnapshotId, string? search, string? recordType, DnsActorContext actor,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginSnapshotAsync(cancellationToken);
        var zone = await inventory.GetZoneAsync(zoneSnapshotId, cancellationToken);
        if (zone is null)
            return new(false, "SnapshotExpired", "The active DNS zone snapshot was not found.");
        var first = await inventory.GetRecordsAsync(
            new(zoneSnapshotId, search, recordType, 1, PageSize), cancellationToken);
        if (first.TotalCount > MaxRecordRows)
            return TooLarge(MaxRecordRows);
        var csv = new CsvDocument(MaxExportCharacters);
        csv.Add(new[] { "Server", "Zone", "Name", "FQDN", "Type", "Value", "TTL Seconds", "Timestamp (UTC)", "Zone Scope", "Virtualization Instance" });
        var complete = await AppendPagesAsync(first, page => inventory.GetRecordsAsync(
                new(zoneSnapshotId, search, recordType, page, PageSize), cancellationToken), record =>
            new[]
            {
                zone.ServerDisplayName, zone.Name, record.RelativeName, record.FullyQualifiedName,
                record.RecordType, record.CanonicalValue, record.TimeToLiveSeconds.ToString(),
                record.Timestamp is null ? null : Utc(record.Timestamp.Value), record.ZoneScope,
                record.VirtualizationInstance,
            }, csv);
        if (!complete) return TooLargeFile();
        var file = File(csv, $"dns-records-{SafeFilePart(zone.Name)}");
        await AuditAsync("DnsRecordsExport", $"Exported {first.TotalCount} DNS record inventory rows for '{zone.Name}'.", actor, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new(true, null, "DNS record inventory exported.", file);
    }

    public async Task<DnsExportResultModel> ExportComparisonAsync(
        DnsComparisonQuery query, DnsActorContext actor,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginSnapshotAsync(cancellationToken);
        var first = await inventory.CompareAsync(query with { PageNumber = 1, PageSize = PageSize }, cancellationToken);
        if (first.TotalCount > MaxComparisonRows)
            return TooLarge(MaxComparisonRows);
        var header = new List<string?> { "Zone", "Name", "Type", "Zone Scope", "Virtualization Instance" };
        foreach (var server in first.Servers)
        {
            header.Add($"{server.ServerDisplayName} - Status");
            header.Add($"{server.ServerDisplayName} - Values");
            if (query.CompareTimeToLive) header.Add($"{server.ServerDisplayName} - TTL Seconds");
        }
        var csv = new CsvDocument(MaxExportCharacters);
        csv.Add(header);
        var complete = await AppendComparisonPagesAsync(first, page => inventory.CompareAsync(
            query with { PageNumber = page, PageSize = PageSize }, cancellationToken), row =>
        {
            var values = new List<string?>
            {
                row.ZoneName, row.RelativeName, row.RecordType, row.ZoneScope, row.VirtualizationInstance,
            };
            foreach (var server in first.Servers)
            {
                var cell = row.Cells.Single(x => x.ServerId == server.ServerId);
                values.Add(cell.Status);
                values.Add(string.Join(" | ", cell.Values));
                if (query.CompareTimeToLive)
                    values.Add(string.Join(" | ", cell.TimeToLiveValues));
            }
            return values;
        }, csv);
        if (!complete) return TooLargeFile();
        var file = File(csv, "dns-comparison");
        await AuditAsync("DnsComparisonExport", $"Exported {first.TotalCount} DNS comparison rows across {first.Servers.Count} servers.", actor, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new(true, null, "DNS comparison exported.", file);
    }

    private static async Task<bool> AppendPagesAsync<T>(
        Application.Common.Models.PagedResult<T> first,
        Func<int, Task<Application.Common.Models.PagedResult<T>>> readPage,
        Func<T, IReadOnlyList<string?>> map,
        CsvDocument csv)
    {
        for (var page = 1; page <= first.TotalPages; page++)
        {
            var result = page == 1 ? first : await readPage(page);
            foreach (var item in result.Items)
                if (!csv.Add(map(item))) return false;
        }
        return true;
    }

    private static async Task<bool> AppendComparisonPagesAsync(
        DnsComparisonResultModel first,
        Func<int, Task<DnsComparisonResultModel>> readPage,
        Func<DnsComparisonRowModel, IReadOnlyList<string?>> map,
        CsvDocument csv)
    {
        for (var page = 1; page <= first.TotalPages; page++)
        {
            var result = page == 1 ? first : await readPage(page);
            foreach (var item in result.Items)
                if (!csv.Add(map(item))) return false;
        }
        return true;
    }

    private async Task<IDbContextTransaction?> BeginSnapshotAsync(CancellationToken cancellationToken)
    {
        return context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;
    }

    private async Task AuditAsync(
        string action, string description, DnsActorContext actor, CancellationToken cancellationToken)
    {
        context.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = "DnsInventory",
            Description = description,
            ActorUserId = actor.UserId,
            ActorUserName = Limit(actor.UserName, 100),
            IpAddress = Limit(actor.IpAddress, 64),
            UserAgent = Limit(actor.UserAgent, 1024),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static DnsExportFileModel File(CsvDocument csv, string name)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv.ToString());
        var content = new byte[preamble.Length + body.Length];
        preamble.CopyTo(content, 0);
        body.CopyTo(content, preamble.Length);
        return new(content, CsvContentType, $"{name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private static string CsvCell(string? value)
    {
        var safe = (value ?? string.Empty).Replace("\0", string.Empty, StringComparison.Ordinal);
        if (safe.TrimStart().StartsWith('=') || safe.TrimStart().StartsWith('+')
            || safe.TrimStart().StartsWith('-') || safe.TrimStart().StartsWith('@'))
            safe = $"'{safe}";
        return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static DnsExportResultModel TooLarge(int limit) =>
        new(false, "ExportLimitExceeded", $"The export exceeds {limit} rows. Narrow the filters and retry.");
    private static DnsExportResultModel TooLargeFile() =>
        new(false, "ExportSizeExceeded", "The export exceeds the safe file-size limit. Narrow the filters and retry.");
    private static string Bool(bool value) => value ? "Yes" : "No";
    private static string Utc(DateTime value) => value.ToUniversalTime().ToString("O");
    private static string SafeFilePart(string value) =>
        new(value.ToLowerInvariant().Select(x => char.IsLetterOrDigit(x) || x is '.' or '-' ? x : '-').Take(80).ToArray());
    private static string? Limit(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }

    private sealed class CsvDocument(int maxCharacters)
    {
        private readonly StringBuilder _content = new();

        public bool Add(IReadOnlyList<string?> row)
        {
            var line = string.Join(',', row.Select(CsvCell));
            if (_content.Length + line.Length + Environment.NewLine.Length > maxCharacters)
                return false;
            _content.AppendLine(line);
            return true;
        }

        public override string ToString() => _content.ToString();
    }
}
