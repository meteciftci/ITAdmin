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

public sealed class DnsZoneTransferManagementServiceTests
{
    [Fact]
    public void Endpoints_require_dedicated_permission()
    {
        foreach (var name in new[] { nameof(DnsManagementAdministrationController.GetZoneTransferConfiguration), nameof(DnsManagementAdministrationController.UpdateZoneTransferConfiguration) })
        {
            var attribute = typeof(DnsManagementAdministrationController).GetMethod(name)!.GetCustomAttribute<RequirePermissionAttribute>();
            Assert.Equal($"Permission:{DnsManagementPermissions.ManageZoneTransfers}", attribute?.Policy);
        }
    }

    [Fact]
    public async Task Read_returns_live_primary_zones_and_token_without_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new ZoneTransferAgent();
        var result = await CreateService(context, agent).GetAsync(server.Id);

        Assert.True(result.Success);
        Assert.Equal("example.com", Assert.Single(result.Configuration!.Zones).ZoneName);
        Assert.NotEmpty(result.Configuration.StateToken);
        Assert.Equal(HostAgentDnsZoneTransferAction.Read, agent.LastRequest!.DnsZoneTransferAction);
        Assert.Empty(context.DnsOperationLogs);
    }

    [Fact]
    public async Task Update_forwards_expected_state_and_writes_sanitized_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new ZoneTransferAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);

        var result = await service.UpdateAsync(new(server.Id, "example.com",
            DnsZoneTransferMode.TransferToSecureServers, ["192.0.2.20"],
            DnsZoneNotifyMode.NotifyServers, ["192.0.2.20"], current.Configuration!.StateToken, Actor));

        Assert.True(result.Success);
        Assert.Equal(HostAgentDnsZoneTransferMode.TransferToSecureServers, agent.LastRequest!.DnsZoneTransferMode);
        Assert.Contains("example.com", agent.LastRequest.DnsExpectedZoneTransferConfigurationJson, StringComparison.Ordinal);
        var log = await context.DnsOperationLogs.SingleAsync();
        Assert.Equal("ZoneTransferUpdate", log.OperationType);
        Assert.Equal("Succeeded", log.Status);
        Assert.DoesNotContain("actual-secret", log.RequestSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_restricted_mode_is_rejected_before_agent_call()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new ZoneTransferAgent();
        var result = await CreateService(context, agent).UpdateAsync(new(server.Id, "example.com",
            DnsZoneTransferMode.TransferToSecureServers, [], DnsZoneNotifyMode.NoNotify, [], "invalid", Actor));

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ErrorCode);
        Assert.Null(agent.LastRequest);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");
    private static DnsZoneTransferManagementService CreateService(AppDbContext context, IHostAgentClient agent) =>
        new(context, new FakeSecretProtector(), agent, NullLogger<DnsZoneTransferManagementService>.Instance);
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

    private sealed class ZoneTransferAgent : IHostAgentClient
    {
        public HostAgentRequest? LastRequest { get; private set; }
        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var configuration = new HostAgentDnsZoneTransferConfiguration
            {
                Zones = [new HostAgentDnsZoneTransferSetting
                {
                    ZoneName = "example.com", TransferMode = HostAgentDnsZoneTransferMode.NoTransfer,
                    NotifyMode = HostAgentDnsZoneNotifyMode.NoNotify,
                }],
            };
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                DnsZoneTransferConfiguration = new HostAgentDnsZoneTransferConfigurationResult
                { Success = true, Message = "ok", Before = configuration, After = configuration },
            });
        }
    }
}
