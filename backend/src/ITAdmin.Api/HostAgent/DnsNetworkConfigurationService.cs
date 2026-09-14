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

public sealed class DnsNetworkConfigurationService(
    AppDbContext context, ISecretProtector secretProtector, IHostAgentClient hostAgentClient,
    ILogger<DnsNetworkConfigurationService> logger) : IDnsNetworkConfigurationService
{
    public Task<DnsNetworkOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(serverId, HostAgentDnsNetworkAction.Read, null, cancellationToken);

    public Task<DnsNetworkOperationModel> MutateAsync(DnsNetworkMutationCommand command, CancellationToken cancellationToken = default) =>
        ExecuteAsync(command.ServerId, command.Action switch
        {
            DnsNetworkAction.UpdateListeningAddresses => HostAgentDnsNetworkAction.UpdateListeningAddresses,
            DnsNetworkAction.AddRootHint => HostAgentDnsNetworkAction.AddRootHint,
            DnsNetworkAction.UpdateRootHint => HostAgentDnsNetworkAction.UpdateRootHint,
            _ => HostAgentDnsNetworkAction.RemoveRootHint,
        }, command, cancellationToken);

    private async Task<DnsNetworkOperationModel> ExecuteAsync(
        Guid serverId, HostAgentDnsNetworkAction action, DnsNetworkMutationCommand? command,
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
                    return Failure("InvalidStateToken", "Refresh the live DNS network configuration and retry.");
                expectedJson = configuration.GetRawText();
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            { return Failure("InvalidStateToken", "Refresh the live DNS network configuration and retry."); }
        }

        string password;
        try { password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword); }
        catch (Exception exception)
        {
            logger.LogError(exception, "The protected DNS credential {CredentialProfileId} could not be read.", server.DnsCredentialProfileId);
            return Failure("CredentialUnavailable", "The assigned credential could not be read. Save the password again and retry.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        HostAgentDnsNetworkConfigurationResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.ManageDnsNetworkConfiguration, CorrelationId = correlationId,
                DnsHostName = server.HostName, DnsPort = server.Port,
                DnsUseSsl = server.Transport == DnsConnectionTransport.Https,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName, DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds, DnsNetworkAction = action,
                DnsListeningIpAddresses = command?.ListeningIpAddresses,
                DnsRootHintNameServer = NormalizeName(command?.RootHintNameServer),
                DnsRootHintIpAddresses = command?.RootHintIpAddresses,
                DnsOriginalRootHintNameServer = NormalizeName(command?.OriginalRootHintNameServer),
                DnsExpectedNetworkConfigurationJson = expectedJson,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnsNetworkConfiguration is not null
                ? response.DnsNetworkConfiguration
                : new() { Success = false, FailureKind = "HostAgentRejected", Message = "The ITAdmin Host Agent rejected the DNS network configuration operation." };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS network configuration on {DnsServerId}.", server.Id);
            result = new() { Success = false, FailureKind = "HostAgentUnavailable", Message = "The ITAdmin Host Agent is unavailable on the portal server." };
        }

        if (command is not null) await WriteAuditAsync(server, command, result, correlationId, cancellationToken);
        return new(result.Success, result.FailureKind, result.Message, result.After is null ? null : Map(server.Id, result.After));
    }

    private async Task WriteAuditAsync(DnsServer server, DnsNetworkMutationCommand command,
        HostAgentDnsNetworkConfigurationResult result, string correlationId, CancellationToken cancellationToken)
    {
        var operation = command.Action switch
        {
            DnsNetworkAction.UpdateListeningAddresses => "DnsListeningAddressesUpdate",
            DnsNetworkAction.AddRootHint => "DnsRootHintAdd",
            DnsNetworkAction.UpdateRootHint => "DnsRootHintUpdate",
            _ => "DnsRootHintRemove",
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
            Status = result.Success ? "Succeeded" : "Failed", RecordName = Limit(command.RootHintNameServer, 253),
            RequestSummaryJson = JsonSerializer.Serialize(new { command.Action, command.ListeningIpAddresses,
                command.RootHintNameServer, command.RootHintIpAddresses, command.OriginalRootHintNameServer }),
            BeforeSnapshotJson = result.Before is null ? null : JsonSerializer.Serialize(result.Before),
            AfterSnapshotJson = result.After is null ? null : JsonSerializer.Serialize(result.After),
            ErrorCode = Limit(result.FailureKind, 64), ErrorMessage = result.Success ? null : Limit(result.Message, 2000),
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024),
            CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Validate(DnsNetworkMutationCommand value)
    {
        if (!Enum.IsDefined(value.Action)) return "A supported DNS network action is required.";
        if (string.IsNullOrWhiteSpace(value.ExpectedStateToken) || value.ExpectedStateToken.Length > 200_000)
            return "Refresh the live DNS network configuration and retry.";
        if (value.Action == DnsNetworkAction.UpdateListeningAddresses
            && (value.ListeningIpAddresses.Count is < 1 or > 64 || value.ListeningIpAddresses.Any(x => !IPAddress.TryParse(x, out _))))
            return "Between 1 and 64 valid listening IP addresses are required.";
        if (value.Action is DnsNetworkAction.AddRootHint or DnsNetworkAction.UpdateRootHint or DnsNetworkAction.RemoveRootHint)
        {
            if (!IsDnsName(value.RootHintNameServer)) return "A valid root name server FQDN is required.";
            if (value.Action != DnsNetworkAction.RemoveRootHint
                && (value.RootHintIpAddresses.Count is < 1 or > 16 || value.RootHintIpAddresses.Any(x => !IPAddress.TryParse(x, out _))))
                return "Between 1 and 16 valid root hint IP addresses are required.";
            if (value.Action == DnsNetworkAction.UpdateRootHint && !IsDnsName(value.OriginalRootHintNameServer))
                return "The original root name server FQDN is required.";
        }
        return null;
    }

    private static bool IsDnsName(string? value)
    {
        var name = value?.Trim().TrimEnd('.');
        return !string.IsNullOrWhiteSpace(name) && name.Length <= 253 && !name.Any(char.IsControl)
            && Uri.CheckHostName(name) == UriHostNameType.Dns;
    }
    private static string? NormalizeName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('.') + ".";

    private static DnsNetworkConfigurationModel Map(Guid serverId, HostAgentDnsNetworkConfiguration value)
    {
        var json = JsonSerializer.Serialize(new { ServerId = serverId, Configuration = value }, HostAgentProtocol.Json);
        return new(value.ListeningIpAddresses, value.AvailableIpAddresses,
            value.RootHints.Select(x => new DnsRootHintModel(x.NameServer, x.IpAddresses)).ToArray(),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    private static string? Limit(string? value, int max)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, max)];
    }
    private static DnsNetworkOperationModel Failure(string code, string message) => new(false, code, message);
}
