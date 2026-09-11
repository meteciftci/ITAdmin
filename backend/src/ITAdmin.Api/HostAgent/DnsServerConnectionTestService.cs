using System.Text.Json;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.HostAgent.Contracts;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Api.HostAgent;

public sealed class DnsServerConnectionTestService(
    AppDbContext context,
    ISecretProtector secretProtector,
    IHostAgentClient hostAgentClient,
    ILogger<DnsServerConnectionTestService> logger) : IDnsServerConnectionTestService
{
    public async Task<DnsAdministrationResult<DnsServerConnectionTestModel>> TestAsync(
        Guid serverId, DnsActorContext actor, CancellationToken cancellationToken = default)
    {
        var server = await context.DnsServers.Include(x => x.CredentialProfile)
            .SingleOrDefaultAsync(x => x.Id == serverId, cancellationToken);
        if (server is null) return new(false, "DNS server was not found.");
        if (!server.CredentialProfile.IsEnabled) return new(false, "The assigned credential profile is disabled.");
        var correlationId = Guid.NewGuid().ToString("N");

        var timeoutSeconds = await context.DnsManagementSettings.AsNoTracking()
            .Select(x => (int?)x.CommandTimeoutSeconds).FirstOrDefaultAsync(cancellationToken) ?? 30;
        string password;
        try
        {
            password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The protected DNS credential {CredentialProfileId} could not be read.",
                server.DnsCredentialProfileId);
            return new(false, "The assigned credential could not be read. Save the password again and retry.");
        }

        HostAgentResponse response;
        try
        {
            response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.TestDnsServerConnection,
                CorrelationId = correlationId,
                DnsHostName = server.HostName,
                DnsPort = server.Port,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls
                    : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName,
                DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = timeoutSeconds,
            }, cancellationToken);
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS server {DnsServerId} connection test.", server.Id);
            var unavailable = Failure(server, "HostAgentUnavailable",
                "The ITAdmin Host Agent is unavailable on the portal server.", hostAgentAvailable: false);
            await WriteAuditAsync(server, unavailable, actor, correlationId, cancellationToken);
            return new(true, unavailable.Message, unavailable);
        }

        if (response.Status is not HostAgentResponseStatus.Ok || response.DnsProbe is null)
        {
            var unavailable = Failure(server, "HostAgentRejected",
                string.IsNullOrWhiteSpace(response.Message)
                    ? "The ITAdmin Host Agent rejected the DNS connection test."
                    : Limit(response.Message, 2000)!,
                hostAgentAvailable: true);
            await WriteAuditAsync(server, unavailable, actor, correlationId, cancellationToken);
            return new(true, unavailable.Message, unavailable);
        }

        var result = Map(server, response.DnsProbe);
        var now = result.TestedAt;
        server.CredentialProfile.LastValidatedAt = now;
        server.CredentialProfile.LastValidationStatus = result.Success ? "Succeeded" : "Failed";
        server.CredentialProfile.LastValidationMessage = result.Message;
        if (result.NetworkReachable) server.LastSeenAt = now;
        if (result.Success)
        {
            server.OperatingSystemVersion = result.OperatingSystemVersion;
            server.DnsServerVersion = result.DnsServerVersion;
            server.CapabilitiesJson = JsonSerializer.Serialize(result.Capabilities);
        }
        await WriteAuditAsync(server, result, actor, correlationId, cancellationToken);
        return new(true, result.Message, result);
    }

    private async Task WriteAuditAsync(DnsServer server, DnsServerConnectionTestModel result,
        DnsActorContext actor, string correlationId, CancellationToken cancellationToken)
    {
        context.AuditLogs.Add(new AuditLog
        {
            Action = "DnsServerConnectionTest", EntityName = "DnsServer", EntityId = server.Id.ToString(),
            Description = $"DNS server '{server.DisplayName}' connection test {(result.Success ? "succeeded" : "failed")} ({result.FailureKind ?? "None"}).",
            ActorUserId = actor.UserId, ActorUserName = actor.UserName,
            IpAddress = actor.IpAddress, UserAgent = actor.UserAgent, CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id, ServerDisplayName = server.DisplayName,
            OperationType = "ServerConnectionTest", Status = result.Success ? "Succeeded" : "Failed",
            ErrorCode = result.FailureKind, ErrorMessage = result.Success ? null : result.Message,
            ActorUserId = actor.UserId, ActorUserName = actor.UserName,
            IpAddress = actor.IpAddress, UserAgent = actor.UserAgent,
            CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static DnsServerConnectionTestModel Map(DnsServer server, HostAgentDnsProbeResult x) => new(
        server.Id, server.DisplayName, x.Success, x.FailureKind, Limit(x.Message, 2000) ?? "The DNS connection test failed.", true,
        x.NetworkReachable, x.TlsValidated, x.AuthenticationSucceeded, x.DnsModuleAvailable,
        x.DnsServiceReachable, x.OperatingSystemVersion, x.PowerShellVersion, x.DnsModuleVersion,
        x.DnsServerVersion, x.ZoneCount, x.Capabilities is null ? null : new(
            x.Capabilities.Zones, x.Capabilities.Records, x.Capabilities.ServerSettings,
            x.Capabilities.Dnssec, x.Capabilities.Policies, x.Capabilities.Scopes, x.Capabilities.Cache),
        DateTime.UtcNow);

    private static DnsServerConnectionTestModel Failure(DnsServer server, string kind, string message,
        bool hostAgentAvailable) => new(server.Id, server.DisplayName, false, kind, message,
            hostAgentAvailable, false, false, false, false, false,
            null, null, null, null, null, null, DateTime.UtcNow);

    private static string? Limit(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
}
