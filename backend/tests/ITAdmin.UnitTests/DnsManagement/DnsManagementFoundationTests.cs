using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ITAdmin.UnitTests.DnsManagement;

public sealed class DnsManagementFoundationTests
{
    [Fact]
    public void Settings_defaults_preserve_cached_inventory_and_prompt_for_fresh_comparison_data()
    {
        var settings = new DnsManagementSettings();

        Assert.True(settings.AutomaticSyncEnabled);
        Assert.True(settings.SyncRecordInventory);
        Assert.True(settings.PromptForFullSyncOnComparisonOpen);
        Assert.Equal(15, settings.DefaultSyncIntervalMinutes);
        Assert.Equal(15, settings.ComparisonSnapshotStaleAfterMinutes);
        Assert.Equal(30, settings.SnapshotRetentionDays);
    }

    [Fact]
    public void Model_allows_only_one_active_snapshot_per_server()
    {
        using var context = CreateModelContext();
        var entity = context.Model.FindEntityType(typeof(DnsInventorySnapshot));

        Assert.NotNull(entity);
        var index = Assert.Single(
            entity.GetIndexes(),
            candidate => candidate.Properties.Select(property => property.Name).SequenceEqual([nameof(DnsInventorySnapshot.DnsServerId)])
                         && candidate.IsUnique);
        Assert.Equal("is_active", index.GetFilter());
    }

    [Fact]
    public void Model_prevents_duplicate_pending_or_running_sync_jobs()
    {
        using var context = CreateModelContext();
        var entity = context.Model.FindEntityType(typeof(DnsSyncJob));

        Assert.NotNull(entity);
        var index = Assert.Single(
            entity.GetIndexes(),
            candidate => candidate.Properties.Select(property => property.Name).SequenceEqual([nameof(DnsSyncJob.DedupeKey)]));
        Assert.True(index.IsUnique);
        Assert.Equal("status IN ('Pending', 'Running')", index.GetFilter());
    }

    [Fact]
    public void Dns_permissions_are_separated_by_operation_risk()
    {
        string[] permissions =
        [
            PermissionCodes.DnsManagement.View,
            PermissionCodes.DnsManagement.ManageSettings,
            PermissionCodes.DnsManagement.Servers.View,
            PermissionCodes.DnsManagement.Servers.Manage,
            PermissionCodes.DnsManagement.Servers.TestConnection,
            PermissionCodes.DnsManagement.Zones.View,
            PermissionCodes.DnsManagement.Zones.Create,
            PermissionCodes.DnsManagement.Zones.Update,
            PermissionCodes.DnsManagement.Zones.Delete,
            PermissionCodes.DnsManagement.Records.View,
            PermissionCodes.DnsManagement.Records.Create,
            PermissionCodes.DnsManagement.Records.Update,
            PermissionCodes.DnsManagement.Records.Delete,
            PermissionCodes.DnsManagement.Compare,
            PermissionCodes.DnsManagement.Export,
            PermissionCodes.DnsManagement.Synchronize,
            PermissionCodes.DnsManagement.ManageServerSettings,
            PermissionCodes.DnsManagement.ManageDnssec,
            PermissionCodes.DnsManagement.ManagePolicies,
            PermissionCodes.DnsManagement.ClearCache,
            PermissionCodes.DnsManagement.ViewOperationLogs,
        ];

        Assert.Equal(permissions.Length, permissions.Distinct(StringComparer.Ordinal).Count());
        Assert.All(permissions, permission => Assert.StartsWith("DnsManagement.", permission, StringComparison.Ordinal));
        Assert.NotEqual(PermissionCodes.DnsManagement.Records.View, PermissionCodes.DnsManagement.Records.Delete);
        Assert.NotEqual(PermissionCodes.DnsManagement.Zones.View, PermissionCodes.DnsManagement.Zones.Delete);
    }

    private static AppDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=itadmin_model_test;Username=test;Password=test")
            .Options;
        return new AppDbContext(options);
    }
}
