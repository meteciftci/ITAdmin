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

public sealed class DnsPolicyManagementService(
    AppDbContext context, ISecretProtector secretProtector, IHostAgentClient hostAgentClient,
    ILogger<DnsPolicyManagementService> logger) : IDnsPolicyManagementService
{
    public Task<DnsPolicyOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(serverId, HostAgentDnsPolicyAction.Read, null, cancellationToken);

    public Task<DnsPolicyOperationModel> MutateAsync(DnsPolicyMutationCommand command, CancellationToken cancellationToken = default) =>
        ExecuteAsync(command.ServerId, Map(command.Action), command, cancellationToken);

    private async Task<DnsPolicyOperationModel> ExecuteAsync(Guid serverId, HostAgentDnsPolicyAction action,
        DnsPolicyMutationCommand? command, CancellationToken cancellationToken)
    {
        var server = await context.DnsServers.Include(x => x.CredentialProfile)
            .SingleOrDefaultAsync(x => x.Id == serverId, cancellationToken);
        if (server is null) return Failure("ServerNotFound", "The DNS server was not found.");
        var settings = await context.DnsManagementSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.IsEnabled) return Failure("DnsManagementDisabled", "DNS Management is disabled.");
        if (!server.IsEnabled || !server.CredentialProfile.IsEnabled) return Failure("ServerDisabled", "The DNS server or its credential profile is disabled.");

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
                    return Failure("InvalidStateToken", "Refresh the live DNS policy configuration and retry.");
                expectedJson = configuration.GetRawText();
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            { return Failure("InvalidStateToken", "Refresh the live DNS policy configuration and retry."); }
        }

        string password;
        try { password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword); }
        catch (Exception exception)
        {
            logger.LogError(exception, "The protected DNS credential {CredentialProfileId} could not be read.", server.DnsCredentialProfileId);
            return Failure("CredentialUnavailable", "The assigned credential could not be read. Save the password again and retry.");
        }

        var mutation = command is null ? null : new HostAgentDnsPolicyMutation
        {
            Name = command.Name.Trim(), ZoneName = Normalize(command.ZoneName), Ipv4Subnets = Normalize(command.Ipv4Subnets), Ipv6Subnets = Normalize(command.Ipv6Subnets),
            Level = (HostAgentDnsPolicyLevel)command.Level, Decision = (HostAgentDnsPolicyDecision)command.Decision,
            Condition = (HostAgentDnsPolicyCondition)command.Condition, ProcessingOrder = command.ProcessingOrder, Enabled = command.Enabled,
            ClientSubnet = Map(command.ClientSubnet), Fqdn = Map(command.Fqdn), QueryType = Map(command.QueryType),
            TransportProtocol = Map(command.TransportProtocol), InternetProtocol = Map(command.InternetProtocol), ServerInterfaceIp = Map(command.ServerInterfaceIp),
            TimeOfDay = Map(command.TimeOfDay),
            ZoneScopes = command.ZoneScopes.Select(x => new HostAgentDnsZoneScopeWeight { Name = x.Name.Trim(), Weight = x.Weight }).ToArray(),
        };
        var correlationId = Guid.NewGuid().ToString("N");
        HostAgentDnsPolicyConfigurationResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.ManageDnsPolicyConfiguration, CorrelationId = correlationId,
                DnsHostName = server.HostName, DnsPort = server.Port,
                DnsUseSsl = server.Transport == DnsConnectionTransport.Https,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName, DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint, DnsTimeoutSeconds = settings.CommandTimeoutSeconds,
                DnsPolicyAction = action, DnsPolicyMutation = mutation, DnsExpectedPolicyConfigurationJson = expectedJson,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnsPolicyConfiguration is not null
                ? response.DnsPolicyConfiguration : new() { Success = false, FailureKind = "HostAgentRejected", Message = "The ITAdmin Host Agent rejected the DNS policy operation." };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS policy operation on {DnsServerId}.", server.Id);
            result = new() { Success = false, FailureKind = "HostAgentUnavailable", Message = "The ITAdmin Host Agent is unavailable on the portal server." };
        }
        if (command is not null) await WriteAuditAsync(server, command, result, correlationId, cancellationToken);
        return new(result.Success, result.FailureKind, result.Message, result.After is null ? null : Map(server.Id, result.After));
    }

    private async Task WriteAuditAsync(DnsServer server, DnsPolicyMutationCommand command,
        HostAgentDnsPolicyConfigurationResult result, string correlationId, CancellationToken cancellationToken)
    {
        var operation = $"Policy{command.Action}";
        context.AuditLogs.Add(new AuditLog { Action = $"Dns{operation}", EntityName = "DnsServer", EntityId = server.Id.ToString(),
            Description = $"DNS {operation} '{command.Name}' on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100), IpAddress = Limit(command.Actor.IpAddress, 64),
            UserAgent = Limit(command.Actor.UserAgent, 1024), CreatedAt = DateTimeOffset.UtcNow });
        context.DnsOperationLogs.Add(new DnsOperationLog { DnsServerId = server.Id, ServerDisplayName = server.DisplayName,
            OperationType = operation, Status = result.Success ? "Succeeded" : "Failed", ZoneName = Normalize(command.ZoneName),
            RequestSummaryJson = JsonSerializer.Serialize(new { command.Action, command.Name, command.ZoneName, command.Level, command.Decision, command.Condition, command.ProcessingOrder, command.Enabled }),
            BeforeSnapshotJson = result.Before is null ? null : JsonSerializer.Serialize(result.Before), AfterSnapshotJson = result.After is null ? null : JsonSerializer.Serialize(result.After),
            ErrorCode = Limit(result.FailureKind, 64), ErrorMessage = result.Success ? null : Limit(result.Message, 2000), ActorUserId = command.Actor.UserId,
            ActorUserName = Limit(command.Actor.UserName, 100), IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024),
            CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Validate(DnsPolicyMutationCommand x)
    {
        if (string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 256 || x.Name.Any(char.IsControl)
            || x.Name.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*', ',', ';']) >= 0) return "A valid file-safe name is required.";
        if (x.Action is DnsPolicyAction.CreateZoneScope or DnsPolicyAction.DeleteZoneScope && string.IsNullOrWhiteSpace(x.ZoneName)) return "A zone is required for the scope.";
        if (x.Action == DnsPolicyAction.SaveClientSubnet && x.Ipv4Subnets.Concat(x.Ipv6Subnets).All(string.IsNullOrWhiteSpace)) return "At least one CIDR subnet is required.";
        if (x.Ipv4Subnets.Any(v => !ValidCidr(v, 32)) || x.Ipv6Subnets.Any(v => !ValidCidr(v, 128))) return "Client subnets must be valid IPv4 or IPv6 CIDR values.";
        var targetsPolicy = x.Action is DnsPolicyAction.SaveQueryPolicy or DnsPolicyAction.DeleteQueryPolicy or DnsPolicyAction.SetQueryPolicyEnabled
            or DnsPolicyAction.SaveZoneTransferPolicy or DnsPolicyAction.DeleteZoneTransferPolicy or DnsPolicyAction.SetZoneTransferPolicyEnabled;
        if (targetsPolicy && x.Level == DnsPolicyLevel.Zone && string.IsNullOrWhiteSpace(x.ZoneName)) return "A zone is required for a zone-level policy.";
        var savesPolicy = x.Action is DnsPolicyAction.SaveQueryPolicy or DnsPolicyAction.SaveZoneTransferPolicy;
        if (savesPolicy && x.Decision == DnsPolicyDecision.Allow && (x.Level == DnsPolicyLevel.Server || x.Action == DnsPolicyAction.SaveZoneTransferPolicy)) return "This policy type cannot use Allow.";
        if (x.Action == DnsPolicyAction.SaveQueryPolicy && new[] { x.ClientSubnet, x.Fqdn, x.QueryType, x.TransportProtocol, x.InternetProtocol, x.ServerInterfaceIp }.All(v => v is null || v.Values.Count == 0)) return "At least one policy criterion is required.";
        if (x.Action == DnsPolicyAction.SaveZoneTransferPolicy && new[] { x.ClientSubnet, x.TransportProtocol, x.InternetProtocol, x.ServerInterfaceIp, x.TimeOfDay }.All(v => v is null || v.Values.Count == 0)) return "At least one zone transfer policy criterion is required.";
        if (x.Action == DnsPolicyAction.SaveZoneTransferPolicy && (x.Fqdn is not null || x.QueryType is not null || x.ZoneScopes.Count > 0)) return "Zone transfer policies contain unsupported criteria.";
        if (savesPolicy && x.ProcessingOrder is < 1 or > 100_000) return "Processing order must be between 1 and 100000.";
        if (savesPolicy && Criteria(x).Any(c => c.Values.Count is < 1 or > 64 || c.Values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 256 || v.Contains(',') || v.Contains(';') || v.Any(char.IsControl)))) return "Policy criteria contain invalid values.";
        if (x.QueryType is not null && x.QueryType.Values.Any(v => v.Trim().ToUpperInvariant() is not ("A" or "AAAA" or "ANY" or "CNAME" or "MX" or "NS" or "PTR" or "SOA" or "SRV" or "TXT"))) return "The query type criterion contains an unsupported value.";
        if (x.TransportProtocol is not null && x.TransportProtocol.Values.Any(v => v.Trim().ToUpperInvariant() is not ("TCP" or "UDP"))) return "Transport protocol must be TCP or UDP.";
        if (x.InternetProtocol is not null && x.InternetProtocol.Values.Any(v => v.Trim().ToUpperInvariant() is not ("IPV4" or "IPV6"))) return "Internet protocol must be IPv4 or IPv6.";
        if (x.ServerInterfaceIp is not null && x.ServerInterfaceIp.Values.Any(v => !IPAddress.TryParse(v.Trim(), out _))) return "Server interface criteria must be valid IP addresses.";
        if (x.ZoneScopes.Count > 0 && (x.Level != DnsPolicyLevel.Zone || x.Decision != DnsPolicyDecision.Allow)) return "Zone scopes require a zone-level Allow policy.";
        if (x.ZoneScopes.Count > 32 || x.ZoneScopes.Any(v => string.IsNullOrWhiteSpace(v.Name) || v.Name.Length > 256 || v.Weight is < 1 or > 10_000)) return "Zone scope weights are invalid.";
        if (string.IsNullOrWhiteSpace(x.ExpectedStateToken) || x.ExpectedStateToken.Length > 800_000) return "Refresh the live DNS policy configuration and retry.";
        return null;
    }
    private static IEnumerable<DnsPolicyCriterionModel> Criteria(DnsPolicyMutationCommand x) =>
        new[] { x.ClientSubnet, x.Fqdn, x.QueryType, x.TransportProtocol, x.InternetProtocol, x.ServerInterfaceIp, x.TimeOfDay }.OfType<DnsPolicyCriterionModel>();
    private static bool ValidCidr(string value, int maxPrefix)
    { var parts = value.Trim().Split('/'); return parts.Length == 2 && IPAddress.TryParse(parts[0], out var address) && int.TryParse(parts[1], out var prefix) && prefix >= 0 && prefix <= maxPrefix && (maxPrefix == 32 ? address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork : address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6); }
    private static HostAgentDnsPolicyCriterion? Map(DnsPolicyCriterionModel? x) => x is null || x.Values.Count == 0 ? null : new() { Operator = (HostAgentDnsPolicyMatchOperator)x.Operator, Values = Normalize(x.Values) };
    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> values) => values.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static HostAgentDnsPolicyAction Map(DnsPolicyAction x) => x switch
    {
        DnsPolicyAction.SaveClientSubnet => HostAgentDnsPolicyAction.SaveClientSubnet,
        DnsPolicyAction.DeleteClientSubnet => HostAgentDnsPolicyAction.DeleteClientSubnet,
        DnsPolicyAction.CreateZoneScope => HostAgentDnsPolicyAction.CreateZoneScope,
        DnsPolicyAction.DeleteZoneScope => HostAgentDnsPolicyAction.DeleteZoneScope,
        DnsPolicyAction.SaveQueryPolicy => HostAgentDnsPolicyAction.SaveQueryPolicy,
        DnsPolicyAction.DeleteQueryPolicy => HostAgentDnsPolicyAction.DeleteQueryPolicy,
        DnsPolicyAction.SetQueryPolicyEnabled => HostAgentDnsPolicyAction.SetQueryPolicyEnabled,
        DnsPolicyAction.SaveZoneTransferPolicy => HostAgentDnsPolicyAction.SaveZoneTransferPolicy,
        DnsPolicyAction.DeleteZoneTransferPolicy => HostAgentDnsPolicyAction.DeleteZoneTransferPolicy,
        DnsPolicyAction.SetZoneTransferPolicyEnabled => HostAgentDnsPolicyAction.SetZoneTransferPolicyEnabled,
        _ => throw new ArgumentOutOfRangeException(nameof(x), x, null),
    };
    private static DnsPolicyConfigurationModel Map(Guid serverId, HostAgentDnsPolicyConfiguration x)
    {
        var json = JsonSerializer.Serialize(new { ServerId = serverId, Configuration = x }, HostAgentProtocol.Json);
        return new(x.ClientSubnets.Select(v => new DnsClientSubnetModel(v.Name, v.Ipv4Subnets, v.Ipv6Subnets)).ToArray(),
            x.ZoneScopes.Select(v => new DnsZoneScopeModel(v.ZoneName, v.Name)).ToArray(),
            x.QueryPolicies.Select(v => new DnsQueryPolicyModel(v.Name, v.Level, v.ZoneName, v.Action, v.Condition, v.ProcessingOrder, v.Enabled, v.ClientSubnet, v.Fqdn, v.QueryType, v.TransportProtocol, v.InternetProtocol, v.ServerInterfaceIp, v.ZoneScope)).ToArray(),
            x.ZoneTransferPolicies.Select(v => new DnsZoneTransferPolicyModel(v.Name, v.Level, v.ZoneName, v.Action, v.Condition, v.ProcessingOrder, v.Enabled, v.ClientSubnet, v.TransportProtocol, v.InternetProtocol, v.ServerInterfaceIp, v.TimeOfDay)).ToArray(),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }
    private static string? Limit(string? value, int max) { var v = Normalize(value); return v is null ? null : v[..Math.Min(v.Length, max)]; }
    private static DnsPolicyOperationModel Failure(string code, string message) => new(false, code, message);
}
