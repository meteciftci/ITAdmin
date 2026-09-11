using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Api.HostAgent;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.HostAgent.Contracts;
using ITAdmin.Persistence.Context;
using ITAdmin.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITAdmin.UnitTests.DnsManagement;

public sealed class DnsInventorySyncServiceTests
{
    [Fact]
    public void Synchronize_endpoint_requires_dedicated_permission()
    {
        var attribute = typeof(DnsManagementAdministrationController)
            .GetMethod(nameof(DnsManagementAdministrationController.SynchronizeServer))!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{DnsManagementPermissions.Synchronize}", attribute?.Policy);
        var batchAttribute = typeof(DnsInventoryController)
            .GetMethod(nameof(DnsInventoryController.SynchronizeAll))!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{DnsManagementPermissions.Synchronize}", batchAttribute?.Policy);
    }

    [Fact]
    public async Task Enqueue_is_idempotent_for_an_active_server_job()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var service = CreateService(context, new InventoryAgent());

        var first = await service.EnqueueAsync(server.Id, Actor);
        var second = await service.EnqueueAsync(server.Id, Actor);

        Assert.True(first.IsSuccess);
        Assert.False(first.Value!.AlreadyQueued);
        Assert.True(second.Value!.AlreadyQueued);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Single(context.DnsSyncJobs);
        Assert.Single(context.AuditLogs);
    }

    [Fact]
    public async Task Enqueue_all_uses_one_batch_and_deduplicates_existing_work()
    {
        await using var context = CreateContext();
        var first = await SeedAsync(context);
        var second = new DnsServer
        {
            DisplayName = "Public DNS",
            HostName = "dns02.example.local",
            Port = 5986,
            DnsCredentialProfileId = first.DnsCredentialProfileId,
            IsEnabled = true,
        };
        context.DnsServers.Add(second);
        await context.SaveChangesAsync();
        var service = CreateService(context, new InventoryAgent());
        await service.EnqueueAsync(first.Id, Actor);

        var result = await service.EnqueueAllEnabledAsync(Actor);

        Assert.Equal(2, result.TargetedCount);
        Assert.Equal(1, result.QueuedCount);
        Assert.Equal(1, result.AlreadyQueuedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, result.Jobs.Count);
        Assert.Equal(result.BatchId, Assert.Single(result.Jobs, x => !x.AlreadyQueued).BatchId);
    }

    [Fact]
    public async Task Completed_sync_atomically_replaces_active_snapshot_and_canonicalizes_records()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var previous = new DnsInventorySnapshot
        {
            DnsServerId = server.Id,
            Scope = DnsSyncScope.FullInventory,
            Trigger = DnsSyncTrigger.Manual,
            Status = DnsSyncStatus.Completed,
            IsActive = true,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            ZoneCount = 1,
            RecordCount = 1,
        };
        context.DnsInventorySnapshots.Add(previous);
        await context.SaveChangesAsync();
        var agent = new InventoryAgent();
        var service = CreateService(context, agent);
        await service.EnqueueAsync(server.Id, Actor);

        Assert.True(await service.ProcessNextAsync());

        var snapshots = await context.DnsInventorySnapshots.AsNoTracking().OrderBy(x => x.StartedAt).ToListAsync();
        Assert.False(snapshots[0].IsActive);
        var active = Assert.Single(snapshots, x => x.IsActive);
        Assert.Equal(DnsSyncStatus.Completed, active.Status);
        Assert.Equal(1, active.ZoneCount);
        Assert.Equal(2, active.RecordCount);
        var records = await context.DnsRecordSnapshots.AsNoTracking().OrderBy(x => x.ZoneScope).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Equal("www.example.local", records[0].FullyQualifiedName);
        Assert.Equal(64, records[0].RecordHash.Length);
        Assert.Equal("{\"IPv4Address\":\"10.0.0.10\"}", records[0].CanonicalValue);
        Assert.Contains(agent.Requests, x => x.DnsInventoryKind == HostAgentDnsInventoryKind.Records && x.DnsZoneScope == "blue");
        var refreshedServer = await context.DnsServers.AsNoTracking().SingleAsync(x => x.Id == server.Id);
        Assert.Equal("Completed", refreshedServer.LastSyncStatus);
        Assert.NotNull(refreshedServer.LastSuccessfulSyncAt);
    }

    [Fact]
    public async Task Failed_sync_never_replaces_last_successful_snapshot()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var previous = new DnsInventorySnapshot
        {
            DnsServerId = server.Id,
            Scope = DnsSyncScope.FullInventory,
            Trigger = DnsSyncTrigger.Manual,
            Status = DnsSyncStatus.Completed,
            IsActive = true,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            ZoneCount = 1,
        };
        context.DnsInventorySnapshots.Add(previous);
        await context.SaveChangesAsync();
        var service = CreateService(context, new InventoryAgent(fail: true));
        await service.EnqueueAsync(server.Id, Actor);

        Assert.True(await service.ProcessNextAsync());

        Assert.True((await context.DnsInventorySnapshots.AsNoTracking().SingleAsync(x => x.Id == previous.Id)).IsActive);
        var failed = await context.DnsInventorySnapshots.AsNoTracking().SingleAsync(x => x.Id != previous.Id);
        Assert.False(failed.IsActive);
        Assert.Equal(DnsSyncStatus.Failed, failed.Status);
        Assert.Equal("DnsInventoryReadFailed", failed.ErrorCode);
        Assert.Equal("Failed", (await context.DnsServers.AsNoTracking().SingleAsync(x => x.Id == server.Id)).LastSyncStatus);
    }

    [Fact]
    public async Task Automatic_scheduler_queues_only_due_servers_and_uses_server_interval_override()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var due = await SeedAsync(context);
        due.SyncIntervalMinutes = 30;
        due.LastSuccessfulSyncAt = now.AddMinutes(-31);
        var recent = new DnsServer
        {
            DisplayName = "Recent DNS",
            HostName = "dns02.example.local",
            Port = 5986,
            DnsCredentialProfileId = due.DnsCredentialProfileId,
            IsEnabled = true,
            LastSuccessfulSyncAt = now.AddMinutes(-14),
        };
        context.DnsServers.Add(recent);
        await context.SaveChangesAsync();

        var queued = await CreateService(context, new InventoryAgent()).EnqueueDueAutomaticAsync(now);

        Assert.Equal(1, queued);
        var job = await context.DnsSyncJobs.AsNoTracking().SingleAsync();
        Assert.Equal(due.Id, job.DnsServerId);
        Assert.Equal(DnsSyncTrigger.Scheduled, job.Trigger);
        Assert.Equal(10, job.Priority);
        Assert.Equal("dns-scheduler", job.RequestedByUserName);
    }

    [Fact]
    public async Task Automatic_scheduler_uses_last_request_to_avoid_a_failure_hot_loop()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var server = await SeedAsync(context);
        server.LastSuccessfulSyncAt = now.AddHours(-1);
        context.DnsSyncJobs.Add(new DnsSyncJob
        {
            BatchId = Guid.NewGuid(),
            DnsServerId = server.Id,
            Scope = DnsSyncScope.FullInventory,
            Trigger = DnsSyncTrigger.Scheduled,
            Status = DnsSyncStatus.Failed,
            DedupeKey = $"{server.Id:N}:inventory:*",
            RequestedAt = now.AddHours(-1),
            CompletedAt = now.AddSeconds(-30),
        });
        await context.SaveChangesAsync();

        var queued = await CreateService(context, new InventoryAgent()).EnqueueDueAutomaticAsync(now);

        Assert.Equal(0, queued);
        Assert.Single(context.DnsSyncJobs);
    }

    [Fact]
    public async Task Automatic_scheduler_does_nothing_when_automatic_sync_is_disabled()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var settings = await context.DnsManagementSettings.SingleAsync();
        settings.AutomaticSyncEnabled = false;
        await context.SaveChangesAsync();

        var queued = await CreateService(context, new InventoryAgent())
            .EnqueueDueAutomaticAsync(DateTime.UtcNow);

        Assert.Equal(0, queued);
        Assert.Empty(context.DnsSyncJobs);
    }

    [Fact]
    public async Task Snapshot_retention_never_deletes_active_running_or_recent_inventory()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var server = await SeedAsync(context);
        var expired = Snapshot(server.Id, DnsSyncStatus.Failed, false, now.AddDays(-31));
        var active = Snapshot(server.Id, DnsSyncStatus.Completed, true, now.AddDays(-31));
        var running = Snapshot(server.Id, DnsSyncStatus.Running, false, now.AddDays(-31));
        var recent = Snapshot(server.Id, DnsSyncStatus.Completed, false, now.AddDays(-29));
        context.DnsInventorySnapshots.AddRange(expired, active, running, recent);
        await context.SaveChangesAsync();

        var purged = await CreateService(context, new InventoryAgent()).PurgeExpiredSnapshotsAsync(now);

        Assert.Equal(1, purged);
        var remaining = await context.DnsInventorySnapshots.AsNoTracking().Select(x => x.Id).ToListAsync();
        Assert.DoesNotContain(expired.Id, remaining);
        Assert.Contains(active.Id, remaining);
        Assert.Contains(running.Id, remaining);
        Assert.Contains(recent.Id, remaining);
    }

    [Fact]
    public async Task Expired_final_lease_marks_partial_snapshot_failed_without_activating_it()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        const string correlationId = "expired-job";
        var job = new DnsSyncJob
        {
            BatchId = Guid.NewGuid(),
            DnsServerId = server.Id,
            Scope = DnsSyncScope.FullInventory,
            Trigger = DnsSyncTrigger.Manual,
            Status = DnsSyncStatus.Running,
            DedupeKey = $"{server.Id:N}:inventory:*",
            Priority = 100,
            AttemptCount = 3,
            RequestedAt = DateTime.UtcNow.AddMinutes(-20),
            StartedAt = DateTime.UtcNow.AddMinutes(-15),
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CorrelationId = correlationId,
        };
        var partial = new DnsInventorySnapshot
        {
            DnsServerId = server.Id,
            Scope = DnsSyncScope.FullInventory,
            Trigger = DnsSyncTrigger.Manual,
            Status = DnsSyncStatus.Running,
            IsActive = false,
            StartedAt = DateTime.UtcNow.AddMinutes(-15),
            CorrelationId = correlationId,
        };
        context.AddRange(job, partial);
        await context.SaveChangesAsync();

        Assert.False(await CreateService(context, new InventoryAgent()).ProcessNextAsync());

        Assert.Equal(DnsSyncStatus.Failed, job.Status);
        Assert.Equal(DnsSyncStatus.Failed, partial.Status);
        Assert.False(partial.IsActive);
        Assert.Equal("LeaseExpired", partial.ErrorCode);
        Assert.Equal("Failed", server.LastSyncStatus);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");

    private static DnsInventorySnapshot Snapshot(
        Guid serverId, DnsSyncStatus status, bool isActive, DateTime completedAt) => new()
        {
            DnsServerId = serverId,
            Scope = DnsSyncScope.FullInventory,
            Trigger = DnsSyncTrigger.Scheduled,
            Status = status,
            IsActive = isActive,
            StartedAt = completedAt.AddMinutes(-1),
            CompletedAt = completedAt,
        };

    private static DnsInventorySyncService CreateService(AppDbContext context, IHostAgentClient agent) =>
        new(context, new FakeSecretProtector(), agent, NullLogger<DnsInventorySyncService>.Instance);

    private static async Task<DnsServer> SeedAsync(AppDbContext context)
    {
        context.DnsManagementSettings.Add(new DnsManagementSettings { IsEnabled = true, SyncRecordInventory = true });
        var credential = new DnsCredentialProfile
        {
            Name = "Internal",
            UserName = "EXAMPLE\\dns-user",
            EncryptedPassword = "protected:actual-secret",
            IsEnabled = true,
        };
        var server = new DnsServer
        {
            DisplayName = "Internal DNS",
            HostName = "dns01.example.local",
            Port = 5986,
            CredentialProfile = credential,
            IsEnabled = true,
        };
        context.DnsServers.Add(server);
        await context.SaveChangesAsync();
        return server;
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class InventoryAgent(bool fail = false) : IHostAgentClient
    {
        public List<HostAgentRequest> Requests { get; } = [];

        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            HostAgentDnsInventoryPage page;
            if (fail)
            {
                page = new() { Success = false, FailureKind = "DnsInventoryReadFailed", Message = "read failed" };
            }
            else if (request.DnsInventoryKind == HostAgentDnsInventoryKind.Zones)
            {
                page = new()
                {
                    Success = true,
                    Message = "ok",
                    Zones = [new HostAgentDnsZoneInventoryItem
                    {
                        Name = "example.local", ZoneType = "Primary", IsDsIntegrated = true,
                        ZoneScopes = ["blue"],
                    }],
                };
            }
            else
            {
                page = new()
                {
                    Success = true,
                    Message = "ok",
                    Records = [new HostAgentDnsRecordInventoryItem
                    {
                        RelativeName = request.DnsZoneScope is null ? "www" : "api",
                        RecordType = "A", RecordDataJson = request.DnsZoneScope is null
                            ? "{\"IPv4Address\":\"10.0.0.10\"}" : "{\"IPv4Address\":\"10.0.0.11\"}",
                        TimeToLiveSeconds = 300, ZoneScope = request.DnsZoneScope,
                    }],
                };
            }
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                Message = "ok",
                DnsInventoryPage = page,
            });
        }
    }
}
