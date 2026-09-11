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
