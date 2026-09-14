using System.Text.Json;
using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Api.HostAgent;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.HostAgent.Contracts;
using ITAdmin.Persistence.Context;
using ITAdmin.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITAdmin.UnitTests.DnsManagement;

public sealed class DnsServerConnectionTestServiceTests
{
    [Fact]
    public void Connection_test_endpoint_requires_its_dedicated_permission()
    {
        var attribute = typeof(DnsManagementAdministrationController)
            .GetMethod(nameof(DnsManagementAdministrationController.TestServerConnection))!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{DnsManagementPermissions.ServersTestConnection}", attribute?.Policy);
    }

    [Fact]
    public async Task Successful_probe_uses_protected_credential_and_persists_safe_capabilities()
    {
        await using var context = CreateContext();
        var server = await SeedServerAsync(context);
        var agent = new RecordingAgent(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            DnsProbe = new HostAgentDnsProbeResult
            {
                Success = true, Message = "Probe succeeded.", NetworkReachable = true,
                TlsValidated = true, AuthenticationSucceeded = true, DnsModuleAvailable = true,
                DnsServiceReachable = true, OperatingSystemVersion = "Windows Server 2025",
                DnsServerVersion = "10.0.1", DnsModuleVersion = "2.0", ZoneCount = 12,
                Capabilities = new HostAgentDnsCapabilities { Zones = true, Records = true, Dnssec = true },
            },
        });
        var service = CreateService(context, agent);

        var result = await service.TestAsync(server.Id, new(null, "admin", null, null));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
        Assert.Equal("actual-secret", agent.LastRequest!.DnsPassword);
        Assert.Equal("dns01.example.local", agent.LastRequest.DnsHostName);
        Assert.True(agent.LastRequest.DnsUseSsl);
        Assert.Equal(45, agent.LastRequest.DnsTimeoutSeconds);
        Assert.Equal("Succeeded", server.CredentialProfile.LastValidationStatus);
        Assert.Equal("Windows Server 2025", server.OperatingSystemVersion);
        Assert.Equal("10.0.1", server.DnsServerVersion);
        Assert.True(JsonDocument.Parse(server.CapabilitiesJson!).RootElement.GetProperty("Zones").GetBoolean());
        Assert.DoesNotContain("actual-secret", context.AuditLogs.Single().Description);
        Assert.DoesNotContain("dns-user", context.AuditLogs.Single().Description);
        Assert.Equal("Succeeded", context.DnsOperationLogs.Single().Status);
    }

    [Fact]
    public async Task Http_server_is_sent_to_the_agent_without_tls()
    {
        await using var context = CreateContext();
        var server = await SeedServerAsync(context);
        server.Transport = DnsConnectionTransport.Http;
        server.Port = 5985;
        await context.SaveChangesAsync();
        var agent = new RecordingAgent(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            DnsProbe = new HostAgentDnsProbeResult { Success = false, Message = "Expected test response." },
        });

        await CreateService(context, agent).TestAsync(server.Id, new(null, "admin", null, null));

        Assert.False(agent.LastRequest!.DnsUseSsl);
        Assert.Equal(5985, agent.LastRequest.DnsPort);
        Assert.Equal(HostAgentDnsAuthenticationMode.Negotiate, agent.LastRequest.DnsAuthenticationMode);
    }

    [Fact]
    public async Task Expected_remote_failure_is_returned_as_diagnostics_and_marks_validation_failed()
    {
        await using var context = CreateContext();
        var server = await SeedServerAsync(context);
        var agent = new RecordingAgent(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            DnsProbe = new HostAgentDnsProbeResult
            {
                Success = false, FailureKind = "TlsValidationFailed", Message = "TLS failed.",
                NetworkReachable = true,
            },
        });

        var result = await CreateService(context, agent).TestAsync(server.Id, new(null, "admin", null, null));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Success);
        Assert.Equal("TlsValidationFailed", result.Value.FailureKind);
        Assert.Equal("Failed", server.CredentialProfile.LastValidationStatus);
        Assert.NotNull(server.LastSeenAt);
    }

    [Fact]
    public async Task Unavailable_host_agent_does_not_blame_the_credential()
    {
        await using var context = CreateContext();
        var server = await SeedServerAsync(context);
        var service = CreateService(context, new ThrowingAgent());

        var result = await service.TestAsync(server.Id, new(null, "admin", null, null));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.HostAgentAvailable);
        Assert.Equal("HostAgentUnavailable", result.Value.FailureKind);
        Assert.Null(server.CredentialProfile.LastValidationStatus);
    }

    private static DnsServerConnectionTestService CreateService(AppDbContext context, IHostAgentClient agent) =>
        new(context, new FakeSecretProtector(), agent, NullLogger<DnsServerConnectionTestService>.Instance);

    private static async Task<DnsServer> SeedServerAsync(AppDbContext context)
    {
        context.DnsManagementSettings.Add(new DnsManagementSettings { CommandTimeoutSeconds = 45 });
        var credential = new DnsCredentialProfile
        {
            Name = "Internal", AuthenticationMode = DnsAuthenticationMode.Negotiate,
            UserName = "EXAMPLE\\dns-user", EncryptedPassword = "protected:actual-secret", IsEnabled = true,
        };
        var server = new DnsServer
        {
            DisplayName = "Internal DNS", HostName = "dns01.example.local", Port = 5986,
            CredentialProfile = credential, IsEnabled = true,
        };
        context.DnsServers.Add(server);
        await context.SaveChangesAsync();
        return server;
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class RecordingAgent(HostAgentResponse response) : IHostAgentClient
    {
        public HostAgentRequest? LastRequest { get; private set; }
        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingAgent : IHostAgentClient
    {
        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default) =>
            throw new HostAgentUnavailableException("not available");
    }
}
