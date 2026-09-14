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

public sealed class DnsZoneDelegationManagementService(
    AppDbContext context, ISecretProtector secretProtector, IHostAgentClient hostAgentClient,
    ILogger<DnsZoneDelegationManagementService> logger) : IDnsZoneDelegationManagementService
{
    public Task<DnsZoneDelegationOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(serverId, null, cancellationToken);

    public Task<DnsZoneDelegationOperationModel> MutateAsync(DnsZoneDelegationMutationCommand command, CancellationToken cancellationToken = default) =>
        ExecuteAsync(command.ServerId, command, cancellationToken);

    private async Task<DnsZoneDelegationOperationModel> ExecuteAsync(Guid serverId, DnsZoneDelegationMutationCommand? command,
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
                    return Failure("InvalidStateToken", "Refresh the live DNS zone delegation configuration and retry.");
                expectedJson = configuration.GetRawText();
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            { return Failure("InvalidStateToken", "Refresh the live DNS zone delegation configuration and retry."); }
        }

        string password;
        try { password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword); }
        catch (Exception exception)
        {
            logger.LogError(exception, "The protected DNS credential {CredentialProfileId} could not be read.", server.DnsCredentialProfileId);
            return Failure("CredentialUnavailable", "The assigned credential could not be read. Save the password again and retry.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        HostAgentDnsZoneDelegationConfigurationResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.ManageDnsZoneDelegations, CorrelationId = correlationId,
                DnsHostName = server.HostName, DnsPort = server.Port,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName, DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds,
                DnsZoneDelegationAction = command is null ? HostAgentDnsZoneDelegationAction.Read : command.Action switch
                {
                    DnsZoneDelegationAction.AddNameServer => HostAgentDnsZoneDelegationAction.AddNameServer,
                    DnsZoneDelegationAction.UpdateNameServerAddresses => HostAgentDnsZoneDelegationAction.UpdateNameServerAddresses,
                    DnsZoneDelegationAction.RemoveNameServer => HostAgentDnsZoneDelegationAction.RemoveNameServer,
                    DnsZoneDelegationAction.DeleteDelegation => HostAgentDnsZoneDelegationAction.DeleteDelegation,
                    _ => throw new ArgumentOutOfRangeException(nameof(command.Action)),
                },
                DnsDelegationParentZoneName = NormalizeName(command?.ParentZoneName),
                DnsDelegationChildZoneName = NormalizeName(command?.ChildZoneName),
                DnsDelegationNameServer = NormalizeName(command?.NameServer),
                DnsDelegationIpAddresses = command?.IpAddresses,
                DnsExpectedZoneDelegationConfigurationJson = expectedJson,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnsZoneDelegationConfiguration is not null
                ? response.DnsZoneDelegationConfiguration
                : new() { Success = false, FailureKind = "HostAgentRejected", Message = "The ITAdmin Host Agent rejected the DNS zone delegation operation." };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS zone delegations on {DnsServerId}.", server.Id);
            result = new() { Success = false, FailureKind = "HostAgentUnavailable", Message = "The ITAdmin Host Agent is unavailable on the portal server." };
        }

        if (command is not null) await WriteAuditAsync(server, command, result, correlationId, cancellationToken);
        return new(result.Success, result.FailureKind, result.Message, result.After is null ? null : Map(server.Id, result.After));
    }

    private async Task WriteAuditAsync(DnsServer server, DnsZoneDelegationMutationCommand command,
        HostAgentDnsZoneDelegationConfigurationResult result, string correlationId, CancellationToken cancellationToken)
    {
        var operation = command.Action switch
        {
            DnsZoneDelegationAction.AddNameServer => "ZoneDelegationNameServerAdd",
            DnsZoneDelegationAction.UpdateNameServerAddresses => "ZoneDelegationNameServerUpdate",
            DnsZoneDelegationAction.RemoveNameServer => "ZoneDelegationNameServerRemove",
            _ => "ZoneDelegationDelete",
        };
        context.AuditLogs.Add(new AuditLog
        {
            Action = $"Dns{operation}", EntityName = "DnsZoneDelegation", EntityId = $"{command.ParentZoneName}/{command.ChildZoneName}",
            Description = $"DNS delegation operation '{command.Action}' for '{command.ChildZoneName}.{command.ParentZoneName}' on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024), CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id, ServerDisplayName = server.DisplayName, OperationType = operation,
            Status = result.Success ? "Succeeded" : "Failed", ZoneName = Limit(command.ParentZoneName, 253),
            RecordName = Limit(command.ChildZoneName, 253), RecordType = "NS",
            RequestSummaryJson = JsonSerializer.Serialize(new { command.Action, command.NameServer, command.IpAddresses }),
            BeforeSnapshotJson = result.Before is null ? null : JsonSerializer.Serialize(result.Before),
            AfterSnapshotJson = result.After is null ? null : JsonSerializer.Serialize(result.After),
            ErrorCode = Limit(result.FailureKind, 64), ErrorMessage = result.Success ? null : Limit(result.Message, 2000),
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024),
            CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Validate(DnsZoneDelegationMutationCommand value)
    {
        if (!Enum.IsDefined(value.Action)) return "Select a supported delegation operation.";
        if (!IsDnsName(value.ParentZoneName)) return "A valid parent DNS zone name is required.";
        if (!IsDnsName(value.ChildZoneName)) return "A valid relative child zone name is required.";
        if (value.ChildZoneName.Trim().TrimEnd('.').EndsWith('.' + value.ParentZoneName.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            return "Enter the child zone name relative to its parent zone.";
        if (value.Action is DnsZoneDelegationAction.AddNameServer or DnsZoneDelegationAction.UpdateNameServerAddresses or DnsZoneDelegationAction.RemoveNameServer && !IsDnsName(value.NameServer))
            return "A valid authoritative name server is required.";
        if (value.Action is DnsZoneDelegationAction.AddNameServer or DnsZoneDelegationAction.UpdateNameServerAddresses
            && (value.IpAddresses.Count is < 1 or > 16 || value.IpAddresses.Any(x => !IPAddress.TryParse(x, out _))))
            return "Glue addresses must contain between 1 and 16 valid IP addresses.";
        if (value.Action is DnsZoneDelegationAction.RemoveNameServer or DnsZoneDelegationAction.DeleteDelegation && value.IpAddresses.Count > 0)
            return "Glue addresses are accepted only when adding or updating a name server.";
        if (string.IsNullOrWhiteSpace(value.ExpectedStateToken) || value.ExpectedStateToken.Length > 400_000)
            return "Refresh the live DNS zone delegation configuration and retry.";
        return null;
    }

    private static bool IsDnsName(string? value)
    {
        var name = value?.Trim().TrimEnd('.');
        return !string.IsNullOrWhiteSpace(name) && name.Length <= 253 && !name.Any(char.IsControl)
            && Uri.CheckHostName(name) == UriHostNameType.Dns;
    }
    private static string? NormalizeName(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('.');
    private static DnsZoneDelegationConfigurationModel Map(Guid serverId, HostAgentDnsZoneDelegationConfiguration value)
    {
        var json = JsonSerializer.Serialize(new { ServerId = serverId, Configuration = value }, HostAgentProtocol.Json);
        return new(value.ParentZones,
            value.Delegations.Select(x => new DnsZoneDelegationModel(x.ParentZoneName, x.ChildZoneName,
                x.NameServers.Select(v => new DnsZoneDelegationNameServerModel(v.NameServer, v.IpAddresses)).ToArray())).ToArray(),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }
    private static string? Limit(string? value, int max)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, max)];
    }
    private static DnsZoneDelegationOperationModel Failure(string code, string message) => new(false, code, message);
}
