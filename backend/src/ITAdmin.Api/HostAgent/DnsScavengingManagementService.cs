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

public sealed class DnsScavengingManagementService(
    AppDbContext context, ISecretProtector secretProtector, IHostAgentClient hostAgentClient,
    ILogger<DnsScavengingManagementService> logger) : IDnsScavengingManagementService
{
    public Task<DnsScavengingOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(serverId, HostAgentDnsScavengingAction.Read, null, cancellationToken);

    public Task<DnsScavengingOperationModel> MutateAsync(DnsScavengingMutationCommand command, CancellationToken cancellationToken = default) =>
        ExecuteAsync(command.ServerId, command.Action switch
        {
            DnsScavengingAction.UpdateServer => HostAgentDnsScavengingAction.UpdateServer,
            DnsScavengingAction.UpdateZone => HostAgentDnsScavengingAction.UpdateZone,
            _ => HostAgentDnsScavengingAction.StartScavenging,
        }, command, cancellationToken);

    private async Task<DnsScavengingOperationModel> ExecuteAsync(
        Guid serverId, HostAgentDnsScavengingAction action, DnsScavengingMutationCommand? command,
        CancellationToken cancellationToken)
    {
        var server = await context.DnsServers.Include(x => x.CredentialProfile)
            .SingleOrDefaultAsync(x => x.Id == serverId, cancellationToken);
        if (server is null) return Failure("ServerNotFound", "The DNS server was not found.");
        var settings = await context.DnsManagementSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.IsEnabled) return Failure("DnsManagementDisabled", "DNS Management is disabled.");
        if (!server.IsEnabled || !server.CredentialProfile.IsEnabled)
            return Failure("ServerDisabled", "The DNS server or its credential profile is disabled.");

        string? expectedJson = null;
        if (command is not null)
        {
            var validation = Validate(command);
            if (validation is not null) return Failure("ValidationFailed", validation);
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(command.ExpectedStateToken));
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("ServerId", out var id) || !id.TryGetGuid(out var tokenId) || tokenId != serverId
                    || !document.RootElement.TryGetProperty("Configuration", out var configuration) || configuration.ValueKind != JsonValueKind.Object)
                    return Failure("InvalidStateToken", "Refresh the live scavenging configuration and retry.");
                expectedJson = configuration.GetRawText();
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            { return Failure("InvalidStateToken", "Refresh the live scavenging configuration and retry."); }
        }

        string password;
        try { password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword); }
        catch (Exception exception)
        {
            logger.LogError(exception, "The protected DNS credential {CredentialProfileId} could not be read.", server.DnsCredentialProfileId);
            return Failure("CredentialUnavailable", "The assigned credential could not be read. Save the password again and retry.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        HostAgentDnsScavengingConfigurationResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.ManageDnsScavenging, CorrelationId = correlationId,
                DnsHostName = server.HostName, DnsPort = server.Port,
                DnsUseSsl = server.Transport == DnsConnectionTransport.Https,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName, DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds, DnsScavengingAction = action,
                DnsScavengingState = command?.ScavengingEnabled,
                DnsScavengingIntervalHours = command?.ScavengingIntervalHours,
                DnsAgingZoneName = command?.ZoneName?.Trim(), DnsZoneAgingEnabled = command?.ZoneAgingEnabled,
                DnsZoneNoRefreshIntervalHours = command?.NoRefreshIntervalHours,
                DnsZoneRefreshIntervalHours = command?.RefreshIntervalHours,
                DnsZoneScavengeServers = command?.ScavengeServers,
                DnsExpectedScavengingConfigurationJson = expectedJson,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnsScavengingConfiguration is not null
                ? response.DnsScavengingConfiguration
                : new() { Success = false, FailureKind = "HostAgentRejected", Message = "The ITAdmin Host Agent rejected the DNS scavenging operation." };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS scavenging on {DnsServerId}.", server.Id);
            result = new() { Success = false, FailureKind = "HostAgentUnavailable", Message = "The ITAdmin Host Agent is unavailable on the portal server." };
        }

        if (command is not null) await WriteAuditAsync(server, command, result, correlationId, cancellationToken);
        return new(result.Success, result.FailureKind, result.Message, result.After is null ? null : Map(server.Id, result.After));
    }

    private async Task WriteAuditAsync(DnsServer server, DnsScavengingMutationCommand command,
        HostAgentDnsScavengingConfigurationResult result, string correlationId, CancellationToken cancellationToken)
    {
        var operation = command.Action switch
        {
            DnsScavengingAction.UpdateServer => "DnsScavengingServerUpdate",
            DnsScavengingAction.UpdateZone => "DnsZoneAgingUpdate",
            _ => "DnsScavengingStart",
        };
        context.AuditLogs.Add(new AuditLog
        {
            Action = operation, EntityName = "DnsServer", EntityId = server.Id.ToString(),
            Description = $"{operation} on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id, ServerDisplayName = server.DisplayName, OperationType = operation,
            Status = result.Success ? "Succeeded" : "Failed", ZoneName = Limit(command.ZoneName, 253),
            RequestSummaryJson = JsonSerializer.Serialize(new { command.Action, command.ScavengingEnabled,
                command.ScavengingIntervalHours, command.ZoneName, command.ZoneAgingEnabled,
                command.NoRefreshIntervalHours, command.RefreshIntervalHours, command.ScavengeServers }),
            BeforeSnapshotJson = result.Before is null ? null : JsonSerializer.Serialize(result.Before),
            AfterSnapshotJson = result.After is null ? null : JsonSerializer.Serialize(result.After),
            ErrorCode = Limit(result.FailureKind, 64), ErrorMessage = result.Success ? null : Limit(result.Message, 2000),
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024),
            CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Validate(DnsScavengingMutationCommand value)
    {
        if (!Enum.IsDefined(value.Action))
            return "A supported scavenging action is required.";
        if (string.IsNullOrWhiteSpace(value.ExpectedStateToken) || value.ExpectedStateToken.Length > 400_000)
            return "Refresh the live scavenging configuration and retry.";
        if (value.Action == DnsScavengingAction.UpdateServer
            && (value.ScavengingEnabled is null || value.ScavengingIntervalHours is null or < 0 or > 8760
                || value.ScavengingEnabled == true && value.ScavengingIntervalHours == 0))
            return "An enabled server requires a scavenging interval between 1 and 8760 hours.";
        if (value.Action == DnsScavengingAction.UpdateZone)
        {
            if (string.IsNullOrWhiteSpace(value.ZoneName) || value.ZoneName.Length > 253 || value.ZoneName.Any(char.IsControl))
                return "A valid DNS zone name is required.";
            if (value.ZoneAgingEnabled is null || value.NoRefreshIntervalHours is null or < 0 or > 8760
                || value.RefreshIntervalHours is null or < 0 or > 8760)
                return "Zone aging state and intervals between 0 and 8760 hours are required.";
            if (value.ScavengeServers.Count > 16 || value.ScavengeServers.Any(x => !IPAddress.TryParse(x, out _)))
                return "Scavenging servers must contain at most 16 IP addresses.";
        }
        return null;
    }

    private static DnsScavengingConfigurationModel Map(Guid serverId, HostAgentDnsScavengingConfiguration value)
    {
        var json = JsonSerializer.Serialize(new { ServerId = serverId, Configuration = value }, HostAgentProtocol.Json);
        return new(value.ScavengingEnabled, value.ScavengingIntervalSeconds,
            value.DefaultNoRefreshIntervalSeconds, value.DefaultRefreshIntervalSeconds, value.LastScavengeTime,
            value.Zones.Select(zone => new DnsZoneAgingModel(zone.Name, zone.ZoneType, zone.AgingEnabled,
                zone.IsEligible, zone.IneligibilityReason, zone.NoRefreshIntervalSeconds,
                zone.RefreshIntervalSeconds, zone.AvailableForScavengeTime, zone.ScavengeServers)).ToArray(),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    private static string? Limit(string? value, int max)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, max)];
    }
    private static DnsScavengingOperationModel Failure(string code, string message) => new(false, code, message);
}
