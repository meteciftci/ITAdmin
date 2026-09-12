using System.Reflection;
using System.Text;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Api.Services;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.UnitTests.DnsManagement;

public sealed class DnsInventoryExportServiceTests
{
    [Fact]
    public void Export_endpoints_require_export_and_data_permissions()
    {
        AssertPolicies(nameof(DnsInventoryController.ExportZones),
            $"Permission:{DnsManagementPermissions.Export}",
            $"{RequireAnyPermissionAttribute.PolicyPrefix}{DnsManagementPermissions.ZonesView}|{DnsManagementPermissions.RecordsView}");
        AssertPolicies(nameof(DnsInventoryController.ExportRecords),
            $"Permission:{DnsManagementPermissions.Export}",
            $"Permission:{DnsManagementPermissions.RecordsView}");
        AssertPolicies(nameof(DnsInventoryController.ExportComparison),
            $"Permission:{DnsManagementPermissions.Export}",
            $"Permission:{DnsManagementPermissions.Compare}");
    }

    [Fact]
    public async Task Zone_export_reads_all_pages_adds_bom_and_blocks_formula_injection()
    {
        await using var context = CreateContext();
        var inventory = new ExportInventory { ZoneCount = 101, FormulaServerName = "=cmd|' /C calc'!A0" };
        var service = new DnsInventoryExportService(inventory, context);

        var result = await service.ExportZonesAsync(null, null, Actor);

        Assert.True(result.Success);
        Assert.Equal(2, inventory.ZonePageCalls);
        Assert.Equal(Encoding.UTF8.GetPreamble(), result.File!.Content[..3]);
        var csv = Encoding.UTF8.GetString(result.File.Content);
        Assert.Contains("\"'=cmd|' /C calc'!A0\"", csv, StringComparison.Ordinal);
        Assert.Equal("DnsZonesExport", (await context.AuditLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task Comparison_export_creates_dynamic_server_columns()
    {
        await using var context = CreateContext();
        var service = new DnsInventoryExportService(new ExportInventory(), context);

        var result = await service.ExportComparisonAsync(new(
            [ExportInventory.ServerId], ["example.test"], true, null, 1, 20), Actor);

        var csv = Encoding.UTF8.GetString(result.File!.Content);
        Assert.Contains("Internal DNS - Status", csv, StringComparison.Ordinal);
        Assert.Contains("Internal DNS - TTL Seconds", csv, StringComparison.Ordinal);
        Assert.Contains("192.0.2.1", csv, StringComparison.Ordinal);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");

    private static void AssertPolicies(string method, params string[] expected)
    {
        var policies = typeof(DnsInventoryController).GetMethod(method)!
            .GetCustomAttributes(inherit: true).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Select(x => x.Policy).Where(x => x is not null).Cast<string>().ToArray();
        Assert.All(expected, value => Assert.Contains(value, policies));
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class ExportInventory : IDnsInventoryQueryService
    {
        public static readonly Guid ServerId = Guid.NewGuid();
        public int ZoneCount { get; init; } = 1;
        public string FormulaServerName { get; init; } = "Internal DNS";
        public int ZonePageCalls { get; private set; }

        public Task<PagedResult<DnsZoneInventoryModel>> GetZonesAsync(
            DnsZoneInventoryQuery query, CancellationToken cancellationToken = default)
        {
            ZonePageCalls++;
            var start = (query.PageNumber - 1) * query.PageSize;
            var count = Math.Max(0, Math.Min(query.PageSize, ZoneCount - start));
            var items = Enumerable.Range(start, count).Select(index => Zone(index)).ToArray();
            return Task.FromResult(new PagedResult<DnsZoneInventoryModel>(
                items, query.PageNumber, query.PageSize, ZoneCount,
                (int)Math.Ceiling(ZoneCount / (double)query.PageSize)));
        }

        public Task<DnsZoneInventoryModel?> GetZoneAsync(Guid zoneSnapshotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DnsZoneInventoryModel?>(Zone(0));

        public Task<PagedResult<DnsRecordInventoryModel>> GetRecordsAsync(
            DnsRecordInventoryQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<DnsRecordInventoryModel>([], 1, query.PageSize, 0, 0));

        public Task<DnsComparisonResultModel> CompareAsync(
            DnsComparisonQuery query, CancellationToken cancellationToken = default)
        {
            var server = new DnsInventoryServerModel(ServerId, "Internal DNS", DnsServerEnvironment.Internal,
                true, Guid.NewGuid(), Guid.NewGuid(), DnsSyncScope.FullInventory, DateTime.UtcNow,
                1, 1, "Completed", null, false, true);
            var row = new DnsComparisonRowModel("example.test", "www", "A", null, null,
                [new(ServerId, "Equal", ["192.0.2.1"], [300])]);
            return Task.FromResult(new DnsComparisonResultModel([server], [row], 1, 100, 1, 1));
        }

        public Task<IReadOnlyList<DnsInventoryServerModel>> GetServersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DnsInventoryServerModel>>([]);
        public Task<DnsComparisonContextModel> GetComparisonContextAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<DnsComparisonZoneModel>> GetComparisonZonesAsync(
            IReadOnlyList<Guid> serverIds, string? search, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private DnsZoneInventoryModel Zone(int index) => new(
            Guid.NewGuid(), Guid.NewGuid(), ServerId, FormulaServerName, DnsServerEnvironment.Internal,
            $"zone-{index}.test", "Primary", false, true, false, false, "Secure", "Domain",
            null, null, null, [], false, [], null, null, 1, DateTime.UtcNow);
    }
}
