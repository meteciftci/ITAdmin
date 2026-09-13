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

public sealed class DnssecManagementServiceTests
{
    [Fact]
    public void Endpoints_require_dedicated_dnssec_permission()
    {
        AssertPermission(nameof(DnsManagementAdministrationController.GetDnssecConfiguration));
        AssertPermission(nameof(DnsManagementAdministrationController.MutateDnssecConfiguration));
    }

    [Fact]
    public async Task Read_returns_live_zone_keys_and_opaque_concurrency_token()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new DnssecAgent();
        var service = CreateService(context, agent);

        var result = await service.GetAsync(server.Id);

        Assert.True(result.Success);
        var zone = Assert.Single(result.Configuration!.Zones);
        Assert.Equal("example.local", zone.Name);
        Assert.True(zone.IsSigned);
        Assert.Single(zone.SigningKeys);
        Assert.NotEmpty(result.Configuration.StateToken);
        Assert.Equal(HostAgentDnssecAction.Read, agent.LastRequest!.DnssecAction);
        Assert.Equal("actual-secret", agent.LastRequest.DnsPassword);
        Assert.Empty(context.DnsOperationLogs);
    }

    [Fact]
    public async Task Rollover_forwards_expected_live_state_and_writes_sanitized_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new DnssecAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);
        var keyId = current.Configuration!.Zones[0].SigningKeys[0].KeyId;

        var result = await service.MutateAsync(new(
            server.Id, DnssecAction.RolloverKeys, "example.local", [keyId],
            null, null, null, null, null, null, null, null,
            current.Configuration.StateToken, Actor));

        Assert.True(result.Success);
        Assert.Contains("example.local", agent.LastRequest!.DnsExpectedDnssecConfigurationJson, StringComparison.Ordinal);
        Assert.Equal(keyId, Assert.Single(agent.LastRequest.DnssecKeyIds!));
        var log = await context.DnsOperationLogs.SingleAsync();
        Assert.Equal("DnssecKeyRollover", log.OperationType);
        Assert.Equal("Succeeded", log.Status);
        Assert.DoesNotContain("actual-secret", log.RequestSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_rollover_selection_is_rejected_before_host_agent_call()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new DnssecAgent();
        var service = CreateService(context, agent);

        var result = await service.MutateAsync(new(
            server.Id, DnssecAction.RolloverKeys, "example.local", [],
            null, null, null, null, null, null, null, null, "invalid", Actor));

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ErrorCode);
        Assert.Null(agent.LastRequest);
    }

    [Fact]
    public async Task Validation_update_forwards_typed_value_and_writes_resolver_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new DnssecAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);

        var result = await service.MutateAsync(new(
            server.Id, DnssecAction.SetValidationEnabled, null, [], false,
            null, null, null, null, null, null, null, current.Configuration!.StateToken, Actor));

        Assert.True(result.Success);
        Assert.False(agent.LastRequest!.DnssecValidationEnabled);
        Assert.Null(agent.LastRequest.DnssecZoneName);
        var log = await context.DnsOperationLogs.SingleAsync();
        Assert.Equal("DnssecValidationUpdate", log.OperationType);
        Assert.Equal("Succeeded", log.Status);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");

    private static void AssertPermission(string methodName)
    {
        var attribute = typeof(DnsManagementAdministrationController).GetMethod(methodName)!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{DnsManagementPermissions.ManageDnssec}", attribute?.Policy);
    }

    private static DnssecManagementService CreateService(AppDbContext context, IHostAgentClient agent) =>
        new(context, new FakeSecretProtector(), agent, NullLogger<DnssecManagementService>.Instance);

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

    private sealed class DnssecAgent : IHostAgentClient
    {
        private static readonly Guid KeyId = Guid.Parse("8cdbf862-559e-4a02-b421-cd345a6254e8");
        public HostAgentRequest? LastRequest { get; private set; }

        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var configuration = new HostAgentDnssecConfiguration
            {
                Zones = [new HostAgentDnssecZone
                {
                    Name = "example.local", ZoneType = "Primary", IsDsIntegrated = true,
                    IsSigned = true, IsEligibleForSigning = true,
                    SigningKeys = [new HostAgentDnssecSigningKey { KeyId = KeyId, KeyType = "KeySigningKey" }],
                }],
                Resolver = new HostAgentDnssecResolverConfiguration
                {
                    ValidationEnabled = true, DirectoryServicesAvailable = true,
                    RootTrustAnchorsUrl = "https://data.iana.org/root-anchors/root-anchors.xml",
                    TrustPoints = [new HostAgentDnssecTrustPoint { Name = ".", State = "Active" }],
                },
            };
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                DnssecConfiguration = new HostAgentDnssecConfigurationResult
                {
                    Success = true, Message = "ok", Before = configuration, After = configuration,
                },
            });
        }
    }
}
