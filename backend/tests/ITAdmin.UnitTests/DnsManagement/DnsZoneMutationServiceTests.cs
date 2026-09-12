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

public sealed class DnsZoneMutationServiceTests
{
    [Fact]
    public void Zone_endpoints_require_separate_mutation_permissions()
    {
        AssertPermission(nameof(DnsInventoryController.CreateZone), DnsManagementPermissions.ZonesCreate);
        AssertPermission(nameof(DnsInventoryController.UpdateZone), DnsManagementPermissions.ZonesUpdate);
        AssertPermission(nameof(DnsInventoryController.DeleteZone), DnsManagementPermissions.ZonesDelete);
    }

    [Fact]
    public async Task Update_uses_live_concurrency_state_writes_audit_and_queues_refresh()
    {
        await using var context = CreateContext();
        var zone = await SeedAsync(context);
        var agent = new ZoneAgent();
        var sync = new DnsInventorySyncService(context, new FakeSecretProtector(), agent,
            NullLogger<DnsInventorySyncService>.Instance);
        var service = new DnsZoneMutationService(context, new FakeSecretProtector(), agent, sync,
            NullLogger<DnsZoneMutationService>.Instance);

        var result = await service.ExecuteAsync(new(
            Guid.Empty, zone.Id, string.Empty, DnsZoneKind.Primary, false,
            "NonsecureAndSecure", null, null, null, [], null, null,
            DnsZoneMutationKind.Update, Actor));

        Assert.True(result.Success);
        Assert.Equal(HostAgentOperation.MutateDnsServerZone, agent.LastRequest!.Operation);
        Assert.Equal("NonsecureAndSecure", agent.LastRequest.DnsZoneDynamicUpdate);
        Assert.Contains("example.local", agent.LastRequest.DnsExpectedZoneStateJson, StringComparison.Ordinal);
        var log = await context.DnsOperationLogs.SingleAsync(x => x.OperationType == "ZoneUpdate");
        Assert.Equal("Succeeded", log.Status);
        Assert.DoesNotContain("actual-secret", log.RequestSummaryJson, StringComparison.Ordinal);
        Assert.Equal(DnsSyncTrigger.PostMutation, (await context.DnsSyncJobs.SingleAsync()).Trigger);
    }

    [Fact]
    public async Task Protected_zone_is_rejected_before_host_agent_call()
    {
        await using var context = CreateContext();
        var zone = await SeedAsync(context, isAutoCreated: true);
        var agent = new ZoneAgent();
        var sync = new DnsInventorySyncService(context, new FakeSecretProtector(), agent,
            NullLogger<DnsInventorySyncService>.Instance);
        var service = new DnsZoneMutationService(context, new FakeSecretProtector(), agent, sync,
            NullLogger<DnsZoneMutationService>.Instance);

        var result = await service.ExecuteAsync(new(
            Guid.Empty, zone.Id, string.Empty, DnsZoneKind.Primary, false,
            null, null, null, null, [], null, null, DnsZoneMutationKind.Delete, Actor));

        Assert.False(result.Success);
        Assert.Equal("ProtectedZone", result.ErrorCode);
        Assert.Null(agent.LastRequest);
    }

    [Fact]
    public async Task Create_conditional_forwarder_requires_valid_master_addresses()
    {
        await using var context = CreateContext();
        var zone = await SeedAsync(context);
        var serverId = zone.InventorySnapshot.DnsServerId;
        var agent = new ZoneAgent();
        var sync = new DnsInventorySyncService(context, new FakeSecretProtector(), agent,
            NullLogger<DnsInventorySyncService>.Instance);
        var service = new DnsZoneMutationService(context, new FakeSecretProtector(), agent, sync,
            NullLogger<DnsZoneMutationService>.Instance);

        var invalid = await service.ExecuteAsync(new(
            serverId, null, "partners.example", DnsZoneKind.Forwarder, false,
            null, null, null, null, ["invalid"], 5, false, DnsZoneMutationKind.Create, Actor));
        var valid = await service.ExecuteAsync(new(
            serverId, null, "partners.example", DnsZoneKind.Forwarder, false,
            null, null, null, null, ["192.0.2.10"], 5, false, DnsZoneMutationKind.Create, Actor));

        Assert.False(invalid.Success);
        Assert.True(valid.Success);
        Assert.Equal("192.0.2.10", Assert.Single(agent.LastRequest!.DnsZoneMasterServers!));
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");

    private static void AssertPermission(string methodName, string permission)
    {
        var attribute = typeof(DnsInventoryController).GetMethod(methodName)!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{permission}", attribute?.Policy);
    }

    private static async Task<DnsZoneSnapshot> SeedAsync(AppDbContext context, bool isAutoCreated = false)
    {
        context.DnsManagementSettings.Add(new DnsManagementSettings
        {
            IsEnabled = true,
            SyncRecordInventory = true,
            CommandTimeoutSeconds = 30,
        });
        var server = new DnsServer
        {
            DisplayName = "Internal DNS",
            HostName = "dns01.example.local",
            Port = 5986,
            IsEnabled = true,
            CredentialProfile = new DnsCredentialProfile
            {
                Name = "DNS",
                UserName = "EXAMPLE\\dns-user",
                EncryptedPassword = "protected:actual-secret",
                IsEnabled = true,
            },
        };
        var snapshot = new DnsInventorySnapshot
        {
            DnsServer = server,
            Scope = DnsSyncScope.FullInventory,
            Trigger = DnsSyncTrigger.Manual,
            Status = DnsSyncStatus.Completed,
            IsActive = true,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            CompletedAt = DateTime.UtcNow,
        };
        var zone = new DnsZoneSnapshot
        {
            InventorySnapshot = snapshot,
            Name = "example.local",
            ZoneType = "Primary",
            DynamicUpdate = "None",
            ZoneFile = "example.local.dns",
            PropertiesJson = $"{{\"zoneScopes\":[],\"isAutoCreated\":{isAutoCreated.ToString().ToLowerInvariant()},\"masterServers\":[]}}",
        };
        context.Add(zone);
        await context.SaveChangesAsync();
        return zone;
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class ZoneAgent : IHostAgentClient
    {
        public HostAgentRequest? LastRequest { get; private set; }

        public Task<HostAgentResponse> SendAsync(
            HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var item = new HostAgentDnsZoneInventoryItem
            {
                Name = request.DnsZoneName!,
                ZoneType = request.DnsZoneKind!.Value.ToString(),
                IsDsIntegrated = request.DnsZoneIsDsIntegrated ?? false,
                DynamicUpdate = request.DnsZoneDynamicUpdate,
                ReplicationScope = request.DnsZoneReplicationScope,
                DirectoryPartitionName = request.DnsZonePartitionName,
                ZoneFile = request.DnsZoneFile,
                MasterServers = request.DnsZoneMasterServers ?? [],
                ForwarderTimeoutSeconds = request.DnsZoneForwarderTimeoutSeconds,
                UseRecursion = request.DnsZoneUseRecursion,
            };
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                Message = "ok",
                DnsZoneMutation = new HostAgentDnsZoneMutationResult
                {
                    Success = true,
                    Message = "DNS zone operation completed.",
                    Before = item,
                    After = item,
                },
            });
        }
    }
}
