using System.Net;
using System.Text;
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

public sealed class DnsServerSettingsService(
    AppDbContext context,
    ISecretProtector secretProtector,
    IHostAgentClient hostAgentClient,
    ILogger<DnsServerSettingsService> logger) : IDnsServerSettingsService
{
    public Task<DnsServerSettingsOperationModel> GetAsync(
        Guid serverId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(serverId, HostAgentDnsServerSettingsAction.Read, null, null, cancellationToken);

    public Task<DnsServerSettingsOperationModel> UpdateAsync(
        DnsServerSettingsCommand command, CancellationToken cancellationToken = default) =>
        ExecuteAsync(command.ServerId, HostAgentDnsServerSettingsAction.Update, command,
            command.Actor, cancellationToken);

    public Task<DnsServerSettingsOperationModel> ClearCacheAsync(
        Guid serverId, DnsActorContext actor, CancellationToken cancellationToken = default) =>
        ExecuteAsync(serverId, HostAgentDnsServerSettingsAction.ClearCache, null, actor, cancellationToken);

    private async Task<DnsServerSettingsOperationModel> ExecuteAsync(
        Guid serverId, HostAgentDnsServerSettingsAction action, DnsServerSettingsCommand? command,
        DnsActorContext? actor, CancellationToken cancellationToken)
    {
        var server = await context.DnsServers.Include(x => x.CredentialProfile)
            .SingleOrDefaultAsync(x => x.Id == serverId, cancellationToken);
        if (server is null) return Failure("ServerNotFound", "The DNS server was not found.");
        var settings = await context.DnsManagementSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.IsEnabled)
            return Failure("DnsManagementDisabled", "DNS Management is disabled.");
        if (!server.IsEnabled || !server.CredentialProfile.IsEnabled)
            return Failure("ServerDisabled", "The DNS server or its credential profile is disabled.");

        string? expectedStateJson = null;
        if (action == HostAgentDnsServerSettingsAction.Update)
        {
            var validation = Validate(command!);
            if (validation is not null) return Failure("ValidationFailed", validation);
            try
            {
                expectedStateJson = Encoding.UTF8.GetString(Convert.FromBase64String(command!.ExpectedStateToken));
                using var document = JsonDocument.Parse(expectedStateJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("ServerId", out var serverIdProperty)
                    || !serverIdProperty.TryGetGuid(out var tokenServerId)
                    || tokenServerId != serverId
                    || !document.RootElement.TryGetProperty("Settings", out var settingsProperty)
                    || settingsProperty.ValueKind != JsonValueKind.Object)
                    return Failure("InvalidStateToken", "Refresh the live DNS server settings and retry.");
                expectedStateJson = settingsProperty.GetRawText();
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            {
                return Failure("InvalidStateToken", "Refresh the live DNS server settings and retry.");
            }
        }

        var forwarderAddresses = command?.ForwarderAddresses
            .Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        string password;
        try
        {
            password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The protected DNS credential {CredentialProfileId} could not be read.",
                server.DnsCredentialProfileId);
            return Failure("CredentialUnavailable", "The assigned credential could not be read. Save the password again and retry.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        HostAgentDnsServerSettingsResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.ManageDnsServerSettings,
                CorrelationId = correlationId,
                DnsHostName = server.HostName,
                DnsPort = server.Port,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName,
                DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds,
                DnsServerSettingsAction = action,
                DnsForwarderAddresses = forwarderAddresses,
                DnsForwarderUseRootHint = command?.ForwarderUseRootHint,
                DnsForwarderTimeoutSeconds = command?.ForwarderTimeoutSeconds,
                DnsForwarderEnableReordering = command?.ForwarderEnableReordering,
                DnsRecursionEnabled = command?.RecursionEnabled,
                DnsRecursionAdditionalTimeoutSeconds = command?.RecursionAdditionalTimeoutSeconds,
                DnsRecursionRetryIntervalSeconds = command?.RecursionRetryIntervalSeconds,
                DnsRecursionTimeoutSeconds = command?.RecursionTimeoutSeconds,
                DnsRecursionSecureResponse = command?.RecursionSecureResponse,
                DnsExpectedServerSettingsJson = expectedStateJson,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnsServerSettings is not null
                ? response.DnsServerSettings
                : new() { Success = false, FailureKind = "HostAgentRejected", Message = "The ITAdmin Host Agent rejected the DNS server operation." };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS server operation on {DnsServerId}.", server.Id);
            result = new() { Success = false, FailureKind = "HostAgentUnavailable", Message = "The ITAdmin Host Agent is unavailable on the portal server." };
        }

        if (action != HostAgentDnsServerSettingsAction.Read)
            await WriteAuditAsync(server, action, command, actor!, result, correlationId, cancellationToken);
        var mapped = result.After is null ? null : Map(server.Id, result.After);
        return new(result.Success, result.FailureKind, result.Message, mapped);
    }

    private async Task WriteAuditAsync(
        DnsServer server, HostAgentDnsServerSettingsAction action, DnsServerSettingsCommand? command,
        DnsActorContext actor, HostAgentDnsServerSettingsResult result, string correlationId,
        CancellationToken cancellationToken)
    {
        var operation = action == HostAgentDnsServerSettingsAction.ClearCache ? "CacheClear" : "ServerSettingsUpdate";
        context.AuditLogs.Add(new AuditLog
        {
            Action = $"Dns{operation}",
            EntityName = "DnsServer",
            EntityId = server.Id.ToString(),
            Description = $"DNS {operation} on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
            ActorUserId = actor.UserId,
            ActorUserName = Limit(actor.UserName, 100),
            IpAddress = Limit(actor.IpAddress, 64),
            UserAgent = Limit(actor.UserAgent, 1024),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id,
            ServerDisplayName = server.DisplayName,
            OperationType = operation,
            Status = result.Success ? "Succeeded" : "Failed",
            RequestSummaryJson = command is null ? null : JsonSerializer.Serialize(new
            {
                ForwarderCount = command.ForwarderAddresses.Count,
                command.ForwarderUseRootHint,
                command.ForwarderTimeoutSeconds,
                command.ForwarderEnableReordering,
                command.RecursionEnabled,
                command.RecursionAdditionalTimeoutSeconds,
                command.RecursionRetryIntervalSeconds,
                command.RecursionTimeoutSeconds,
                command.RecursionSecureResponse,
            }),
            BeforeSnapshotJson = result.Before is null ? null : JsonSerializer.Serialize(result.Before),
            AfterSnapshotJson = result.After is null ? null : JsonSerializer.Serialize(result.After),
            ErrorCode = Limit(result.FailureKind, 64),
            ErrorMessage = result.Success ? null : Limit(result.Message, 2000),
            ActorUserId = actor.UserId,
            ActorUserName = Limit(actor.UserName, 100),
            IpAddress = Limit(actor.IpAddress, 64),
            UserAgent = Limit(actor.UserAgent, 1024),
            CorrelationId = correlationId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Validate(DnsServerSettingsCommand command)
    {
        if (command.ForwarderAddresses.Count > 16
            || command.ForwarderAddresses.Any(x => !IPAddress.TryParse(x.Trim(), out _)))
            return "Forwarders must contain at most 16 valid IP addresses.";
        if (command.ForwarderTimeoutSeconds is < 0 or > 15
            || command.RecursionAdditionalTimeoutSeconds is < 0 or > 15
            || command.RecursionRetryIntervalSeconds is < 1 or > 15
            || command.RecursionTimeoutSeconds is < 1 or > 15)
            return "DNS timeout values must be within the supported 0-15 second ranges.";
        if (string.IsNullOrWhiteSpace(command.ExpectedStateToken) || command.ExpectedStateToken.Length > 24_000)
            return "Refresh the live DNS server settings and retry.";
        return null;
    }

    private static DnsServerSettingsModel Map(Guid serverId, HostAgentDnsServerSettings value)
    {
        var stateJson = JsonSerializer.Serialize(new { ServerId = serverId, Settings = value }, HostAgentProtocol.Json);
        return new(value.ForwarderAddresses, value.ForwarderUseRootHint,
            value.ForwarderTimeoutSeconds, value.ForwarderEnableReordering,
            value.RecursionEnabled, value.RecursionAdditionalTimeoutSeconds,
            value.RecursionRetryIntervalSeconds, value.RecursionTimeoutSeconds,
            value.RecursionSecureResponse, Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson)));
    }

    private static string? Limit(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }

    private static DnsServerSettingsOperationModel Failure(string code, string message) =>
        new(false, code, message);
}
