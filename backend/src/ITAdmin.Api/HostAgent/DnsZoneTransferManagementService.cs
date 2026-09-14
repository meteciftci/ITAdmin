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

public sealed class DnsZoneTransferManagementService(
    AppDbContext context, ISecretProtector secretProtector, IHostAgentClient hostAgentClient,
    ILogger<DnsZoneTransferManagementService> logger) : IDnsZoneTransferManagementService
{
    public Task<DnsZoneTransferOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(serverId, null, cancellationToken);

    public Task<DnsZoneTransferOperationModel> UpdateAsync(DnsZoneTransferMutationCommand command, CancellationToken cancellationToken = default) =>
        ExecuteAsync(command.ServerId, command, cancellationToken);

    private async Task<DnsZoneTransferOperationModel> ExecuteAsync(Guid serverId, DnsZoneTransferMutationCommand? command,
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
                    return Failure("InvalidStateToken", "Refresh the live DNS zone transfer configuration and retry.");
                expectedJson = configuration.GetRawText();
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            { return Failure("InvalidStateToken", "Refresh the live DNS zone transfer configuration and retry."); }
        }

        string password;
        try { password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword); }
        catch (Exception exception)
        {
            logger.LogError(exception, "The protected DNS credential {CredentialProfileId} could not be read.", server.DnsCredentialProfileId);
            return Failure("CredentialUnavailable", "The assigned credential could not be read. Save the password again and retry.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        HostAgentDnsZoneTransferConfigurationResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.ManageDnsZoneTransfers, CorrelationId = correlationId,
                DnsHostName = server.HostName, DnsPort = server.Port,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName, DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds,
                DnsZoneTransferAction = command is null ? HostAgentDnsZoneTransferAction.Read : HostAgentDnsZoneTransferAction.Update,
                DnsZoneName = NormalizeName(command?.ZoneName),
                DnsZoneTransferMode = command is null ? null : (HostAgentDnsZoneTransferMode)command.TransferMode,
                DnsZoneSecondaryServers = command?.SecondaryServers,
                DnsZoneNotifyMode = command is null ? null : (HostAgentDnsZoneNotifyMode)command.NotifyMode,
                DnsZoneNotifyServers = command?.NotifyServers,
                DnsExpectedZoneTransferConfigurationJson = expectedJson,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnsZoneTransferConfiguration is not null
                ? response.DnsZoneTransferConfiguration
                : new() { Success = false, FailureKind = "HostAgentRejected", Message = "The ITAdmin Host Agent rejected the DNS zone transfer operation." };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS zone transfers on {DnsServerId}.", server.Id);
            result = new() { Success = false, FailureKind = "HostAgentUnavailable", Message = "The ITAdmin Host Agent is unavailable on the portal server." };
        }

        if (command is not null) await WriteAuditAsync(server, command, result, correlationId, cancellationToken);
        return new(result.Success, result.FailureKind, result.Message, result.After is null ? null : Map(server.Id, result.After));
    }

    private async Task WriteAuditAsync(DnsServer server, DnsZoneTransferMutationCommand command,
        HostAgentDnsZoneTransferConfigurationResult result, string correlationId, CancellationToken cancellationToken)
    {
        context.AuditLogs.Add(new AuditLog
        {
            Action = "DnsZoneTransferUpdate", EntityName = "DnsZone", EntityId = command.ZoneName,
            Description = $"DNS zone transfer settings for '{command.ZoneName}' on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024), CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id, ServerDisplayName = server.DisplayName, OperationType = "ZoneTransferUpdate",
            Status = result.Success ? "Succeeded" : "Failed", ZoneName = Limit(command.ZoneName, 253),
            RequestSummaryJson = JsonSerializer.Serialize(new { command.TransferMode, command.SecondaryServers, command.NotifyMode, command.NotifyServers }),
            BeforeSnapshotJson = result.Before is null ? null : JsonSerializer.Serialize(result.Before),
            AfterSnapshotJson = result.After is null ? null : JsonSerializer.Serialize(result.After),
            ErrorCode = Limit(result.FailureKind, 64), ErrorMessage = result.Success ? null : Limit(result.Message, 2000),
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024),
            CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Validate(DnsZoneTransferMutationCommand value)
    {
        if (!IsDnsName(value.ZoneName)) return "A valid primary DNS zone name is required.";
        if (!Enum.IsDefined(value.TransferMode) || !Enum.IsDefined(value.NotifyMode)) return "Select supported transfer and notification modes.";
        if (value.SecondaryServers.Count > 32 || value.SecondaryServers.Any(x => !IPAddress.TryParse(x, out _)))
            return "Secondary servers must contain at most 32 valid IP addresses.";
        if (value.NotifyServers.Count > 32 || value.NotifyServers.Any(x => !IPAddress.TryParse(x, out _)))
            return "Notification servers must contain at most 32 valid IP addresses.";
        if (value.TransferMode == DnsZoneTransferMode.TransferToSecureServers && value.SecondaryServers.Count == 0)
            return "At least one allowed secondary server is required.";
        if (value.NotifyMode == DnsZoneNotifyMode.NotifyServers && value.NotifyServers.Count == 0)
            return "At least one notification server is required.";
        if (string.IsNullOrWhiteSpace(value.ExpectedStateToken) || value.ExpectedStateToken.Length > 400_000)
            return "Refresh the live DNS zone transfer configuration and retry.";
        return null;
    }

    private static bool IsDnsName(string? value)
    {
        var name = value?.Trim().TrimEnd('.');
        return !string.IsNullOrWhiteSpace(name) && name.Length <= 253 && !name.Any(char.IsControl)
            && Uri.CheckHostName(name) == UriHostNameType.Dns;
    }
    private static string? NormalizeName(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('.');
    private static DnsZoneTransferConfigurationModel Map(Guid serverId, HostAgentDnsZoneTransferConfiguration value)
    {
        var json = JsonSerializer.Serialize(new { ServerId = serverId, Configuration = value }, HostAgentProtocol.Json);
        return new(value.Zones.Select(x => new DnsZoneTransferSettingModel(x.ZoneName, x.IsDsIntegrated,
            (DnsZoneTransferMode)x.TransferMode, x.SecondaryServers, (DnsZoneNotifyMode)x.NotifyMode, x.NotifyServers)).ToArray(),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }
    private static string? Limit(string? value, int max)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, max)];
    }
    private static DnsZoneTransferOperationModel Failure(string code, string message) => new(false, code, message);
}
