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

public sealed class DnsZoneDelegationManagementServiceTests
{
    [Fact]
    public void Endpoints_require_dedicated_permission()
    {
        foreach (var name in new[] { nameof(DnsManagementAdministrationController.GetZoneDelegationConfiguration), nameof(DnsManagementAdministrationController.MutateZoneDelegationConfiguration) })
        {
            var attribute = typeof(DnsManagementAdministrationController).GetMethod(name)!.GetCustomAttribute<RequirePermissionAttribute>();
            Assert.Equal($"Permission:{DnsManagementPermissions.ManageZoneDelegations}", attribute?.Policy);
        }
    }

    [Fact]
    public async Task Read_returns_live_delegations_and_server_bound_token_without_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new DelegationAgent();
        var result = await CreateService(context, agent).GetAsync(server.Id);

        Assert.True(result.Success);
        Assert.Equal("example.com", Assert.Single(result.Configuration!.ParentZones));
        Assert.Equal("child", Assert.Single(result.Configuration.Delegations).ChildZoneName);
        Assert.NotEmpty(result.Configuration.StateToken);
        Assert.Equal(HostAgentDnsZoneDelegationAction.Read, agent.LastRequest!.DnsZoneDelegationAction);
        Assert.Empty(context.DnsOperationLogs);
    }

    [Fact]
    public async Task Add_forwards_typed_values_expected_state_and_writes_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new DelegationAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);

        var result = await service.MutateAsync(new(server.Id, DnsZoneDelegationAction.AddNameServer,
            "example.com", "south", "ns1.south.example.com", ["192.0.2.20"], current.Configuration!.StateToken, Actor));

        Assert.True(result.Success);
        Assert.Equal(HostAgentDnsZoneDelegationAction.AddNameServer, agent.LastRequest!.DnsZoneDelegationAction);
        Assert.Equal(["192.0.2.20"], agent.LastRequest.DnsDelegationIpAddresses);
        Assert.Contains("example.com", agent.LastRequest.DnsExpectedZoneDelegationConfigurationJson, StringComparison.Ordinal);
        var log = await context.DnsOperationLogs.SingleAsync();
        Assert.Equal("ZoneDelegationNameServerAdd", log.OperationType);
        Assert.Equal("Succeeded", log.Status);
        Assert.DoesNotContain("actual-secret", log.RequestSummaryJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("south.example.com", "ns1.south.example.com", "192.0.2.20")]
    [InlineData("south", "bad name", "192.0.2.20")]
    [InlineData("south", "ns1.south.example.com", "not-an-ip")]
    public async Task Invalid_add_is_rejected_before_agent_call(string child, string nameServer, string address)
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new DelegationAgent();
        var result = await CreateService(context, agent).MutateAsync(new(server.Id, DnsZoneDelegationAction.AddNameServer,
            "example.com", child, nameServer, [address], "token", Actor));

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ErrorCode);
        Assert.Null(agent.LastRequest);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");
    private static DnsZoneDelegationManagementService CreateService(AppDbContext context, IHostAgentClient agent) =>
        new(context, new FakeSecretProtector(), agent, NullLogger<DnsZoneDelegationManagementService>.Instance);
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private static async Task<DnsServer> SeedAsync(AppDbContext context)
    {
        context.DnsManagementSettings.Add(new DnsManagementSettings { IsEnabled = true, CommandTimeoutSeconds = 120 });
        var server = new DnsServer
        {
            DisplayName = "Public DNS", HostName = "dns01.example.net", Port = 5986, IsEnabled = true,
            CredentialProfile = new DnsCredentialProfile { Name = "DNS", UserName = "dns-user", EncryptedPassword = "protected:actual-secret", IsEnabled = true },
        };
        context.Add(server);
        await context.SaveChangesAsync();
        return server;
    }

    private sealed class DelegationAgent : IHostAgentClient
    {
        public HostAgentRequest? LastRequest { get; private set; }
        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var configuration = new HostAgentDnsZoneDelegationConfiguration
            {
                ParentZones = ["example.com"],
                Delegations = [new HostAgentDnsZoneDelegation
                {
                    ParentZoneName = "example.com", ChildZoneName = "child",
                    NameServers = [new HostAgentDnsZoneDelegationNameServer { NameServer = "ns1.child.example.com", IpAddresses = ["192.0.2.10"] }],
                }],
            };
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                DnsZoneDelegationConfiguration = new HostAgentDnsZoneDelegationConfigurationResult
                { Success = true, Message = "ok", Before = configuration, After = configuration },
            });
        }
    }
}
