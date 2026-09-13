using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Api.HostAgent;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.HostAgent.Contracts;
using ITAdmin.Persistence.Context;
using ITAdmin.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITAdmin.UnitTests.DnsManagement;

public sealed class DnsScavengingManagementServiceTests
{
    [Fact]
    public void Endpoints_require_dedicated_scavenging_permission()
    {
        AssertPermission(nameof(DnsManagementAdministrationController.GetScavengingConfiguration));
        AssertPermission(nameof(DnsManagementAdministrationController.MutateScavengingConfiguration));
    }

    [Fact]
    public async Task Read_returns_live_settings_and_opaque_concurrency_token_without_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new ScavengingAgent();
        var service = CreateService(context, agent);

        var result = await service.GetAsync(server.Id);

        Assert.True(result.Success);
        Assert.True(result.Configuration!.ScavengingEnabled);
        Assert.Equal("example.local", Assert.Single(result.Configuration.Zones).Name);
        Assert.NotEmpty(result.Configuration.StateToken);
        Assert.Equal(HostAgentDnsScavengingAction.Read, agent.LastRequest!.DnsScavengingAction);
        Assert.Equal("actual-secret", agent.LastRequest.DnsPassword);
        Assert.Empty(context.DnsOperationLogs);
    }

    [Fact]
    public async Task Zone_update_forwards_expected_live_state_and_writes_sanitized_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new ScavengingAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);

        var result = await service.MutateAsync(new(
            server.Id, DnsScavengingAction.UpdateZone, null, null, "example.local", true,
            72, 96, ["192.0.2.10"], current.Configuration!.StateToken, Actor));

        Assert.True(result.Success);
        Assert.Equal(HostAgentDnsScavengingAction.UpdateZone, agent.LastRequest!.DnsScavengingAction);
        Assert.Contains("example.local", agent.LastRequest.DnsExpectedScavengingConfigurationJson, StringComparison.Ordinal);
        Assert.Equal("192.0.2.10", Assert.Single(agent.LastRequest.DnsZoneScavengeServers!));
        var log = await context.DnsOperationLogs.SingleAsync();
        Assert.Equal("DnsZoneAgingUpdate", log.OperationType);
        Assert.Equal("Succeeded", log.Status);
        Assert.DoesNotContain("actual-secret", log.RequestSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Manual_start_writes_destructive_operation_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new ScavengingAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);

        var result = await service.MutateAsync(new(
            server.Id, DnsScavengingAction.StartScavenging, null, null, null, null,
            null, null, [], current.Configuration!.StateToken, Actor));

        Assert.True(result.Success);
        Assert.Equal(HostAgentDnsScavengingAction.StartScavenging, agent.LastRequest!.DnsScavengingAction);
        Assert.Equal("DnsScavengingStart", (await context.DnsOperationLogs.SingleAsync()).OperationType);
    }

    [Fact]
    public async Task Invalid_zone_address_is_rejected_before_host_agent_call()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new ScavengingAgent();
        var service = CreateService(context, agent);

        var result = await service.MutateAsync(new(
            server.Id, DnsScavengingAction.UpdateZone, null, null, "example.local", true,
            168, 168, ["not-an-ip"], "invalid", Actor));

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ErrorCode);
        Assert.Null(agent.LastRequest);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");

    private static void AssertPermission(string methodName)
    {
        var attribute = typeof(DnsManagementAdministrationController).GetMethod(methodName)!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{DnsManagementPermissions.ManageScavenging}", attribute?.Policy);
    }

    private static DnsScavengingManagementService CreateService(AppDbContext context, IHostAgentClient agent) =>
        new(context, new FakeSecretProtector(), agent, NullLogger<DnsScavengingManagementService>.Instance);

    private static async Task<DnsServer> SeedAsync(AppDbContext context)
    {
        context.DnsManagementSettings.Add(new DnsManagementSettings { IsEnabled = true, CommandTimeoutSeconds = 120 });
        var server = new DnsServer
        {
            DisplayName = "Internal DNS", HostName = "dns01.example.local", Port = 5986, IsEnabled = true,
            CredentialProfile = new DnsCredentialProfile
            {
                Name = "DNS", UserName = "EXAMPLE\\dns-user",
                EncryptedPassword = "protected:actual-secret", IsEnabled = true,
            },
        };
        context.Add(server);
        await context.SaveChangesAsync();
        return server;
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class ScavengingAgent : IHostAgentClient
    {
        public HostAgentRequest? LastRequest { get; private set; }

        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var configuration = new HostAgentDnsScavengingConfiguration
            {
                ScavengingEnabled = true,
                ScavengingIntervalSeconds = 604800,
                DefaultNoRefreshIntervalSeconds = 604800,
                DefaultRefreshIntervalSeconds = 604800,
                Zones = [new HostAgentDnsZoneAging
                {
                    Name = "example.local", ZoneType = "Primary", AgingEnabled = true, IsEligible = true,
                    NoRefreshIntervalSeconds = 604800, RefreshIntervalSeconds = 604800,
                    ScavengeServers = ["192.0.2.10"],
                }],
            };
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                DnsScavengingConfiguration = new HostAgentDnsScavengingConfigurationResult
                {
                    Success = true, Message = "ok", Before = configuration, After = configuration,
                },
            });
        }
    }
}
