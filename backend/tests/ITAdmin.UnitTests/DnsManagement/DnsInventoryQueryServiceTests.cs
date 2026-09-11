using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.UnitTests.DnsManagement;

public sealed class DnsInventoryQueryServiceTests
{
    [Fact]
    public void Inventory_endpoints_require_zone_and_record_permissions()
    {
        var zones = typeof(DnsInventoryController).GetMethod(nameof(DnsInventoryController.GetZones))!
            .GetCustomAttribute<RequireAnyPermissionAttribute>();
        var records = typeof(DnsInventoryController).GetMethod(nameof(DnsInventoryController.GetRecords))!
            .GetCustomAttribute<RequirePermissionAttribute>();

        Assert.Equal(
            $"{RequireAnyPermissionAttribute.PolicyPrefix}{DnsManagementPermissions.ZonesView}|{DnsManagementPermissions.RecordsView}",
            zones?.Policy);
        Assert.Equal($"Permission:{DnsManagementPermissions.RecordsView}", records?.Policy);
    }

    [Fact]
    public async Task Server_overview_reports_active_snapshot_availability_and_freshness()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        context.DnsManagementSettings.Add(new DnsManagementSettings { ComparisonSnapshotStaleAfterMinutes = 15 });
        var available = Server("Internal DNS", "dns01.example.local");
        var unavailable = Server("Public DNS", "dns02.example.local");
        context.DnsServers.AddRange(available, unavailable);
        context.DnsInventorySnapshots.Add(new DnsInventorySnapshot
        {
            DnsServer = available,
            Scope = DnsSyncScope.FullInventory,
            Trigger = DnsSyncTrigger.Scheduled,
            Status = DnsSyncStatus.Completed,
            IsActive = true,
            StartedAt = now.AddMinutes(-21),
            CompletedAt = now.AddMinutes(-20),
            ZoneCount = 2,
            RecordCount = 8,
        });
        await context.SaveChangesAsync();

        var result = await new DnsInventoryQueryService(context).GetServersAsync();

        Assert.Equal(2, result.Count);
        var first = Assert.Single(result, x => x.ServerId == available.Id);
        Assert.True(first.IsAvailable);
        Assert.True(first.IsStale);
        Assert.Equal(2, first.ZoneCount);
        Assert.False(Assert.Single(result, x => x.ServerId == unavailable.Id).IsAvailable);
    }

    [Fact]
    public async Task Zone_list_reads_only_active_snapshots_and_parses_scopes()
    {
        await using var context = CreateContext();
        var server = Server("Internal DNS", "dns01.example.local");
        var active = Snapshot(server, true, DateTime.UtcNow);
        var inactive = Snapshot(server, false, DateTime.UtcNow.AddHours(-1));
        var activeZone = Zone(active, "example.local", "{\"zoneScopes\":[\"blue\",\"green\"]}");
        var inactiveZone = Zone(inactive, "old.example.local", null);
        activeZone.Records.Add(Record(activeZone, "www", "A", "{\"IPv4Address\":\"10.0.0.1\"}"));
        context.AddRange(activeZone, inactiveZone);
        await context.SaveChangesAsync();

        var result = await new DnsInventoryQueryService(context).GetZonesAsync(
            new DnsZoneInventoryQuery(server.Id, "EXAMPLE", 1, 20));

        var zone = Assert.Single(result.Items);
        Assert.Equal(activeZone.Id, zone.Id);
        Assert.Equal(["blue", "green"], zone.ZoneScopes);
        Assert.Equal(1, zone.RecordCount);
    }

    [Fact]
    public async Task Record_list_filters_type_and_never_exposes_inactive_snapshot_records()
    {
        await using var context = CreateContext();
        var server = Server("Internal DNS", "dns01.example.local");
        var activeZone = Zone(Snapshot(server, true, DateTime.UtcNow), "example.local", null);
        var inactiveZone = Zone(Snapshot(server, false, DateTime.UtcNow.AddHours(-1)), "example.local", null);
        activeZone.Records.Add(Record(activeZone, "www", "A", "{\"IPv4Address\":\"10.0.0.1\"}"));
        activeZone.Records.Add(Record(activeZone, "mail", "MX", "{\"MailExchange\":\"mx.example.local\"}"));
        inactiveZone.Records.Add(Record(inactiveZone, "old", "A", "{\"IPv4Address\":\"10.0.0.2\"}"));
        context.AddRange(activeZone, inactiveZone);
        await context.SaveChangesAsync();
        var service = new DnsInventoryQueryService(context);

        var result = await service.GetRecordsAsync(
            new DnsRecordInventoryQuery(activeZone.Id, "10.0.0", "a", 1, 20));
        var inactiveResult = await service.GetRecordsAsync(
            new DnsRecordInventoryQuery(inactiveZone.Id, null, null, 1, 20));

        Assert.Equal(activeZone.Records.First().Id, Assert.Single(result.Items).Id);
        Assert.Empty(inactiveResult.Items);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static DnsServer Server(string name, string host) => new()
    {
        DisplayName = name,
        HostName = host,
        Port = 5986,
        IsEnabled = true,
    };

    private static DnsInventorySnapshot Snapshot(DnsServer server, bool active, DateTime completedAt) => new()
    {
        DnsServer = server,
        Scope = DnsSyncScope.FullInventory,
        Trigger = DnsSyncTrigger.Manual,
        Status = DnsSyncStatus.Completed,
        IsActive = active,
        StartedAt = completedAt.AddMinutes(-1),
        CompletedAt = completedAt,
    };

    private static DnsZoneSnapshot Zone(DnsInventorySnapshot snapshot, string name, string? properties) => new()
    {
        InventorySnapshot = snapshot,
        Name = name,
        ZoneType = "Primary",
        PropertiesJson = properties,
    };

    private static DnsRecordSnapshot Record(
        DnsZoneSnapshot zone, string relativeName, string type, string value) => new()
        {
            ZoneSnapshot = zone,
            RelativeName = relativeName,
            FullyQualifiedName = $"{relativeName}.{zone.Name}",
            RecordType = type,
            CanonicalValue = value,
            RecordDataJson = value,
            TimeToLiveSeconds = 300,
            RecordHash = Guid.NewGuid().ToString("N").PadRight(64, '0'),
        };
}
