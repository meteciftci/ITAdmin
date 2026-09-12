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

public sealed class DnsRecordMutationServiceTests
{
    [Fact]
    public void Record_endpoints_require_separate_mutation_permissions()
    {
        AssertPermission(nameof(DnsInventoryController.CreateRecord), DnsManagementPermissions.RecordsCreate);
        AssertPermission(nameof(DnsInventoryController.UpdateRecord), DnsManagementPermissions.RecordsUpdate);
        AssertPermission(nameof(DnsInventoryController.DeleteRecord), DnsManagementPermissions.RecordsDelete);
    }

    [Fact]
    public async Task Update_checks_cached_hash_writes_audit_and_queues_post_mutation_inventory()
    {
        await using var context = CreateContext();
        var seeded = await SeedAsync(context);
        var agent = new MutationAgent();
        var sync = new DnsInventorySyncService(context, new FakeSecretProtector(), agent,
            NullLogger<DnsInventorySyncService>.Instance);
        var service = new DnsRecordMutationService(context, new FakeSecretProtector(), agent, sync,
            NullLogger<DnsRecordMutationService>.Instance);

        var result = await service.ExecuteAsync(new(
            seeded.Zone.Id, seeded.Record.Id, string.Empty, string.Empty, ["10.0.0.20"], 600,
            null, seeded.Record.RecordHash, DnsRecordMutationKind.Update, Actor));

        Assert.True(result.Success);
        Assert.Equal(HostAgentOperation.MutateDnsServerResourceRecord, agent.LastRequest!.Operation);
        Assert.Equal(HostAgentDnsRecordMutationKind.Update, agent.LastRequest.DnsRecordMutationKind);
        Assert.Equal("10.0.0.20", Assert.Single(agent.LastRequest.DnsRecordValues!));
        Assert.Equal(seeded.Record.RecordHash, agent.LastRequest.DnsExpectedRecordHash);
        Assert.Equal(seeded.Record.RecordDataJson, agent.LastRequest.DnsExpectedRecordDataJson);
        Assert.Equal(seeded.Record.TimeToLiveSeconds, agent.LastRequest.DnsExpectedRecordTimeToLiveSeconds);
        var log = await context.DnsOperationLogs.SingleAsync(x => x.OperationType == "RecordUpdate");
        Assert.Equal("Succeeded", log.Status);
        Assert.DoesNotContain("actual-secret", log.RequestSummaryJson, StringComparison.Ordinal);
        var job = await context.DnsSyncJobs.SingleAsync();
        Assert.Equal(DnsSyncTrigger.PostMutation, job.Trigger);
        Assert.Equal(200, job.Priority);
        Assert.NotNull(result.Synchronization);
    }

    [Fact]
    public async Task Stale_expected_hash_is_rejected_before_the_host_agent_is_called()
    {
        await using var context = CreateContext();
        var seeded = await SeedAsync(context);
        var agent = new MutationAgent();
        var sync = new DnsInventorySyncService(context, new FakeSecretProtector(), agent,
            NullLogger<DnsInventorySyncService>.Instance);
        var service = new DnsRecordMutationService(context, new FakeSecretProtector(), agent, sync,
            NullLogger<DnsRecordMutationService>.Instance);

        var result = await service.ExecuteAsync(new(
            seeded.Zone.Id, seeded.Record.Id, string.Empty, string.Empty, ["10.0.0.20"], 600,
            null, new string('b', 64), DnsRecordMutationKind.Update, Actor));

        Assert.False(result.Success);
        Assert.Equal("RecordChanged", result.ErrorCode);
        Assert.Null(agent.LastRequest);
        Assert.Empty(context.DnsOperationLogs);
        Assert.Empty(context.DnsSyncJobs);
    }

    [Fact]
    public async Task Unsupported_record_type_is_read_only()
    {
        await using var context = CreateContext();
        var seeded = await SeedAsync(context, "SOA");
        var agent = new MutationAgent();
        var sync = new DnsInventorySyncService(context, new FakeSecretProtector(), agent,
            NullLogger<DnsInventorySyncService>.Instance);
        var service = new DnsRecordMutationService(context, new FakeSecretProtector(), agent, sync,
            NullLogger<DnsRecordMutationService>.Instance);

        var result = await service.ExecuteAsync(new(
            seeded.Zone.Id, seeded.Record.Id, string.Empty, string.Empty, ["value"], 600,
            null, seeded.Record.RecordHash, DnsRecordMutationKind.Update, Actor));

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ErrorCode);
        Assert.Null(agent.LastRequest);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");

    private static void AssertPermission(string methodName, string permission)
    {
        var attribute = typeof(DnsInventoryController).GetMethod(methodName)!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{permission}", attribute?.Policy);
    }

    private static async Task<(DnsZoneSnapshot Zone, DnsRecordSnapshot Record)> SeedAsync(
        AppDbContext context, string recordType = "A")
    {
        context.DnsManagementSettings.Add(new DnsManagementSettings
        {
            IsEnabled = true,
            SyncRecordInventory = true,
            CommandTimeoutSeconds = 30,
        });
        var credential = new DnsCredentialProfile
        {
            Name = "DNS",
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
            PropertiesJson = "{\"zoneScopes\":[]}",
        };
        var record = new DnsRecordSnapshot
        {
            ZoneSnapshot = zone,
            RelativeName = "www",
            FullyQualifiedName = "www.example.local",
            RecordType = recordType,
            CanonicalValue = "{\"IPv4Address\":\"10.0.0.10\"}",
            RecordDataJson = "{\"IPv4Address\":\"10.0.0.10\"}",
            TimeToLiveSeconds = 300,
            RecordHash = new string('a', 64),
        };
        context.AddRange(snapshot, zone, record);
        await context.SaveChangesAsync();
        return (zone, record);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class MutationAgent : IHostAgentClient
    {
        public HostAgentRequest? LastRequest { get; private set; }

        public Task<HostAgentResponse> SendAsync(
            HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var item = new HostAgentDnsRecordInventoryItem
            {
                RelativeName = request.DnsRecordRelativeName!,
                RecordType = request.DnsRecordType!,
                RecordDataJson = "{\"IPv4Address\":\"10.0.0.20\"}",
                TimeToLiveSeconds = request.DnsRecordTimeToLiveSeconds ?? 0,
                ZoneScope = request.DnsZoneScope,
                VirtualizationInstance = request.DnsVirtualizationInstance,
            };
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                Message = "ok",
                DnsRecordMutation = new HostAgentDnsRecordMutationResult
                {
                    Success = true,
                    Message = "DNS record updated.",
                    Before = item,
                    After = item,
                },
            });
        }
    }
}
