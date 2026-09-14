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

public sealed class DnsNetworkConfigurationServiceTests
{
    [Fact]
    public void Endpoints_require_dedicated_network_configuration_permission()
    {
        AssertPermission(nameof(DnsManagementAdministrationController.GetNetworkConfiguration));
        AssertPermission(nameof(DnsManagementAdministrationController.MutateNetworkConfiguration));
    }

    [Fact]
    public async Task Read_returns_live_addresses_root_hints_and_opaque_token_without_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new NetworkAgent();
        var service = CreateService(context, agent);

        var result = await service.GetAsync(server.Id);

        Assert.True(result.Success);
        Assert.Equal("192.0.2.53", Assert.Single(result.Configuration!.ListeningIpAddresses));
        Assert.Equal("a.root-servers.net.", Assert.Single(result.Configuration.RootHints).NameServer);
        Assert.NotEmpty(result.Configuration.StateToken);
        Assert.Equal(HostAgentDnsNetworkAction.Read, agent.LastRequest!.DnsNetworkAction);
        Assert.Equal("actual-secret", agent.LastRequest.DnsPassword);
        Assert.Empty(context.DnsOperationLogs);
    }

    [Fact]
    public async Task Root_hint_update_forwards_expected_state_and_writes_sanitized_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new NetworkAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);

        var result = await service.MutateAsync(new(server.Id, DnsNetworkAction.UpdateRootHint, [],
            "b.root-servers.net", ["199.9.14.201"], "a.root-servers.net.",
            current.Configuration!.StateToken, Actor));

        Assert.True(result.Success);
        Assert.Equal(HostAgentDnsNetworkAction.UpdateRootHint, agent.LastRequest!.DnsNetworkAction);
        Assert.Equal("b.root-servers.net.", agent.LastRequest.DnsRootHintNameServer);
        Assert.Contains("a.root-servers.net", agent.LastRequest.DnsExpectedNetworkConfigurationJson, StringComparison.Ordinal);
        var log = await context.DnsOperationLogs.SingleAsync();
        Assert.Equal("DnsRootHintUpdate", log.OperationType);
        Assert.Equal("Succeeded", log.Status);
        Assert.DoesNotContain("actual-secret", log.RequestSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Listening_update_forwards_typed_addresses_and_writes_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new NetworkAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);

        var result = await service.MutateAsync(new(server.Id, DnsNetworkAction.UpdateListeningAddresses,
            ["192.0.2.53", "2001:db8::53"], null, [], null, current.Configuration!.StateToken, Actor));

        Assert.True(result.Success);
        Assert.Equal(2, agent.LastRequest!.DnsListeningIpAddresses!.Count);
        Assert.Equal("DnsListeningAddressesUpdate", (await context.DnsOperationLogs.SingleAsync()).OperationType);
    }

    [Fact]
    public async Task Invalid_root_hint_is_rejected_before_host_agent_call()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new NetworkAgent();
        var service = CreateService(context, agent);

        var result = await service.MutateAsync(new(server.Id, DnsNetworkAction.AddRootHint, [],
            "bad name", ["not-an-ip"], null, "invalid", Actor));

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ErrorCode);
        Assert.Null(agent.LastRequest);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");

    private static void AssertPermission(string methodName)
    {
        var attribute = typeof(DnsManagementAdministrationController).GetMethod(methodName)!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{DnsManagementPermissions.ManageNetworkConfiguration}", attribute?.Policy);
    }

    private static DnsNetworkConfigurationService CreateService(AppDbContext context, IHostAgentClient agent) =>
        new(context, new FakeSecretProtector(), agent, NullLogger<DnsNetworkConfigurationService>.Instance);

    private static async Task<DnsServer> SeedAsync(AppDbContext context)
    {
        context.DnsManagementSettings.Add(new DnsManagementSettings { IsEnabled = true, CommandTimeoutSeconds = 120 });
        var server = new DnsServer
        {
            DisplayName = "Public DNS", HostName = "dns01.example.net", Port = 5986, IsEnabled = true,
            CredentialProfile = new DnsCredentialProfile
            {
                Name = "DNS", UserName = "dns-user", EncryptedPassword = "protected:actual-secret", IsEnabled = true,
            },
        };
        context.Add(server);
        await context.SaveChangesAsync();
        return server;
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class NetworkAgent : IHostAgentClient
    {
        public HostAgentRequest? LastRequest { get; private set; }

        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var configuration = new HostAgentDnsNetworkConfiguration
            {
                ListeningIpAddresses = ["192.0.2.53"],
                AvailableIpAddresses = ["192.0.2.53", "2001:db8::53"],
                RootHints = [new HostAgentDnsRootHint { NameServer = "a.root-servers.net.", IpAddresses = ["198.41.0.4"] }],
            };
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                DnsNetworkConfiguration = new HostAgentDnsNetworkConfigurationResult
                {
                    Success = true, Message = "ok", Before = configuration, After = configuration,
                },
            });
        }
    }
}
