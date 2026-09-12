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

public sealed class DnsServerSettingsServiceTests
{
    [Fact]
    public void Endpoints_require_separate_settings_and_cache_permissions()
    {
        AssertPermission(nameof(DnsManagementAdministrationController.GetServerSettings),
            DnsManagementPermissions.ManageServerSettings);
        AssertPermission(nameof(DnsManagementAdministrationController.UpdateServerSettings),
            DnsManagementPermissions.ManageServerSettings);
        AssertPermission(nameof(DnsManagementAdministrationController.ClearServerCache),
            DnsManagementPermissions.ClearCache);
    }

    [Fact]
    public async Task Read_returns_live_settings_and_opaque_concurrency_token()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new SettingsAgent();
        var service = CreateService(context, agent);

        var result = await service.GetAsync(server.Id);

        Assert.True(result.Success);
        Assert.Equal(["192.0.2.10"], result.Settings!.ForwarderAddresses);
        Assert.NotEmpty(result.Settings.StateToken);
        Assert.Equal(HostAgentDnsServerSettingsAction.Read, agent.LastRequest!.DnsServerSettingsAction);
        Assert.Equal("actual-secret", agent.LastRequest.DnsPassword);
        Assert.Empty(context.DnsOperationLogs);
    }

    [Fact]
    public async Task Update_forwards_expected_live_state_and_writes_sanitized_audit()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new SettingsAgent();
        var service = CreateService(context, agent);
        var current = await service.GetAsync(server.Id);

        var result = await service.UpdateAsync(new(
            server.Id, ["198.51.100.10"], false, 5, true,
            true, 4, 3, 8, true, current.Settings!.StateToken, Actor));

        Assert.True(result.Success);
        Assert.Contains("192.0.2.10", agent.LastRequest!.DnsExpectedServerSettingsJson, StringComparison.Ordinal);
        Assert.Equal("198.51.100.10", Assert.Single(agent.LastRequest.DnsForwarderAddresses!));
        var log = await context.DnsOperationLogs.SingleAsync();
        Assert.Equal("ServerSettingsUpdate", log.OperationType);
        Assert.Equal("Succeeded", log.Status);
        Assert.DoesNotContain("actual-secret", log.RequestSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clear_cache_uses_typed_action_and_is_audited()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new SettingsAgent();
        var service = CreateService(context, agent);

        var result = await service.ClearCacheAsync(server.Id, Actor);

        Assert.True(result.Success);
        Assert.Equal(HostAgentDnsServerSettingsAction.ClearCache, agent.LastRequest!.DnsServerSettingsAction);
        Assert.Equal("CacheClear", (await context.DnsOperationLogs.SingleAsync()).OperationType);
    }

    [Fact]
    public async Task Invalid_settings_are_rejected_before_host_agent_call()
    {
        await using var context = CreateContext();
        var server = await SeedAsync(context);
        var agent = new SettingsAgent();
        var service = CreateService(context, agent);

        var result = await service.UpdateAsync(new(
            server.Id, ["not-an-ip"], false, 5, true,
            true, 4, 3, 8, true, "invalid", Actor));

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ErrorCode);
        Assert.Null(agent.LastRequest);
    }

    private static readonly DnsActorContext Actor = new(null, "admin", "127.0.0.1", "unit-test");

    private static void AssertPermission(string methodName, string permission)
    {
        var attribute = typeof(DnsManagementAdministrationController).GetMethod(methodName)!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{permission}", attribute?.Policy);
    }

    private static DnsServerSettingsService CreateService(AppDbContext context, IHostAgentClient agent) =>
        new(context, new FakeSecretProtector(), agent, NullLogger<DnsServerSettingsService>.Instance);

    private static async Task<DnsServer> SeedAsync(AppDbContext context)
    {
        context.DnsManagementSettings.Add(new DnsManagementSettings
        {
            IsEnabled = true,
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
        context.Add(server);
        await context.SaveChangesAsync();
        return server;
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class SettingsAgent : IHostAgentClient
    {
        public HostAgentRequest? LastRequest { get; private set; }

        public Task<HostAgentResponse> SendAsync(
            HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var settings = request.DnsServerSettingsAction == HostAgentDnsServerSettingsAction.Update
                ? new HostAgentDnsServerSettings
                {
                    ForwarderAddresses = request.DnsForwarderAddresses ?? [],
                    ForwarderUseRootHint = request.DnsForwarderUseRootHint ?? false,
                    ForwarderTimeoutSeconds = request.DnsForwarderTimeoutSeconds ?? 5,
                    ForwarderEnableReordering = request.DnsForwarderEnableReordering ?? true,
                    RecursionEnabled = request.DnsRecursionEnabled ?? true,
                    RecursionAdditionalTimeoutSeconds = request.DnsRecursionAdditionalTimeoutSeconds ?? 4,
                    RecursionRetryIntervalSeconds = request.DnsRecursionRetryIntervalSeconds ?? 3,
                    RecursionTimeoutSeconds = request.DnsRecursionTimeoutSeconds ?? 8,
                    RecursionSecureResponse = request.DnsRecursionSecureResponse ?? true,
                }
                : new HostAgentDnsServerSettings
                {
                    ForwarderAddresses = ["192.0.2.10"],
                    ForwarderUseRootHint = true,
                    ForwarderTimeoutSeconds = 5,
                    ForwarderEnableReordering = true,
                    RecursionEnabled = true,
                    RecursionAdditionalTimeoutSeconds = 4,
                    RecursionRetryIntervalSeconds = 3,
                    RecursionTimeoutSeconds = 8,
                    RecursionSecureResponse = true,
                };
            return Task.FromResult(new HostAgentResponse
            {
                Status = HostAgentResponseStatus.Ok,
                DnsServerSettings = new HostAgentDnsServerSettingsResult
                {
                    Success = true,
                    Message = "ok",
                    Before = settings,
                    After = settings,
                },
            });
        }
    }
}
