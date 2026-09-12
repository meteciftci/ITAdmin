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

public sealed class DnssecManagementService(
    AppDbContext context, ISecretProtector secretProtector, IHostAgentClient hostAgentClient,
    ILogger<DnssecManagementService> logger) : IDnssecManagementService
{
    public Task<DnssecOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(serverId, HostAgentDnssecAction.Read, null, cancellationToken);

    public Task<DnssecOperationModel> MutateAsync(DnssecMutationCommand command, CancellationToken cancellationToken = default) =>
        ExecuteAsync(command.ServerId, Map(command.Action), command, cancellationToken);

    private async Task<DnssecOperationModel> ExecuteAsync(Guid serverId, HostAgentDnssecAction action,
        DnssecMutationCommand? command, CancellationToken cancellationToken)
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
                    return Failure("InvalidStateToken", "Refresh the live DNSSEC configuration and retry.");
                expectedJson = configuration.GetRawText();
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            { return Failure("InvalidStateToken", "Refresh the live DNSSEC configuration and retry."); }
        }

        string password;
        try { password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword); }
        catch (Exception exception)
        {
            logger.LogError(exception, "The protected DNS credential {CredentialProfileId} could not be read.", server.DnsCredentialProfileId);
            return Failure("CredentialUnavailable", "The assigned credential could not be read. Save the password again and retry.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        HostAgentDnssecConfigurationResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.ManageDnssecConfiguration, CorrelationId = correlationId,
                DnsHostName = server.HostName, DnsPort = server.Port,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName, DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds, DnssecAction = action,
                DnssecZoneName = command?.ZoneName.Trim(), DnssecKeyIds = command?.KeyIds,
                DnsExpectedDnssecConfigurationJson = expectedJson,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnssecConfiguration is not null
                ? response.DnssecConfiguration
                : new() { Success = false, FailureKind = "HostAgentRejected", Message = "The ITAdmin Host Agent rejected the DNSSEC operation." };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNSSEC operation on {DnsServerId}.", server.Id);
            result = new() { Success = false, FailureKind = "HostAgentUnavailable", Message = "The ITAdmin Host Agent is unavailable on the portal server." };
        }

        if (command is not null) await WriteAuditAsync(server, command, result, correlationId, cancellationToken);
        return new(result.Success, result.FailureKind, result.Message,
            result.After is null ? null : Map(server.Id, result.After));
    }

    private async Task WriteAuditAsync(DnsServer server, DnssecMutationCommand command,
        HostAgentDnssecConfigurationResult result, string correlationId, CancellationToken cancellationToken)
    {
        var operation = command.Action switch
        {
            DnssecAction.SignWithDefaults => "DnssecZoneSign",
            DnssecAction.Resign => "DnssecZoneResign",
            DnssecAction.Unsign => "DnssecZoneUnsign",
            _ => "DnssecKeyRollover",
        };
        context.AuditLogs.Add(new AuditLog
        {
            Action = operation, EntityName = "DnsServer", EntityId = server.Id.ToString(),
            Description = $"DNSSEC {command.Action} '{command.ZoneName}' on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024), CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id, ServerDisplayName = server.DisplayName, OperationType = operation,
            Status = result.Success ? "Succeeded" : "Failed", ZoneName = command.ZoneName.Trim(),
            RequestSummaryJson = JsonSerializer.Serialize(new { command.Action, command.ZoneName, command.KeyIds }),
            BeforeSnapshotJson = result.Before is null ? null : JsonSerializer.Serialize(result.Before),
            AfterSnapshotJson = result.After is null ? null : JsonSerializer.Serialize(result.After),
            ErrorCode = Limit(result.FailureKind, 64), ErrorMessage = result.Success ? null : Limit(result.Message, 2000),
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024),
            CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Validate(DnssecMutationCommand value)
    {
        if (string.IsNullOrWhiteSpace(value.ZoneName) || value.ZoneName.Length > 253 || value.ZoneName.Any(char.IsControl))
            return "A valid DNS zone name is required.";
        if (string.IsNullOrWhiteSpace(value.ExpectedStateToken) || value.ExpectedStateToken.Length > 800_000)
            return "Refresh the live DNSSEC configuration and retry.";
        if (value.Action == DnssecAction.RolloverKeys
            && (value.KeyIds.Count is < 1 or > 8 || value.KeyIds.Any(x => x == Guid.Empty)
                || value.KeyIds.Distinct().Count() != value.KeyIds.Count))
            return "Select between one and eight unique signing keys.";
        if (value.Action != DnssecAction.RolloverKeys && value.KeyIds.Count > 0)
            return "Signing keys are only accepted for rollover.";
        return null;
    }

    private static HostAgentDnssecAction Map(DnssecAction value) => value switch
    {
        DnssecAction.SignWithDefaults => HostAgentDnssecAction.SignWithDefaults,
        DnssecAction.Resign => HostAgentDnssecAction.Resign,
        DnssecAction.Unsign => HostAgentDnssecAction.Unsign,
        _ => HostAgentDnssecAction.RolloverKeys,
    };

    private static DnssecConfigurationModel Map(Guid serverId, HostAgentDnssecConfiguration value)
    {
        var json = JsonSerializer.Serialize(new { ServerId = serverId, Configuration = value }, HostAgentProtocol.Json);
        return new(value.Zones.Select(zone => new DnssecZoneModel(
            zone.Name, zone.ZoneType, zone.IsDsIntegrated, zone.IsAutoCreated, zone.IsSigned,
            zone.IsEligibleForSigning, zone.IneligibilityReason, zone.IsKeyMasterServer,
            zone.KeyMasterServer, zone.KeyMasterStatus, zone.DenialOfExistence,
            zone.Nsec3Iterations, zone.Nsec3OptOut, zone.DnsKeyRecordSetTtlSeconds,
            zone.DsRecordSetTtlSeconds, zone.DsRecordGenerationAlgorithms,
            zone.ParentHasSecureDelegation, zone.SigningKeys.Select(key => new DnssecSigningKeyModel(
                key.KeyId, key.KeyType, key.CryptoAlgorithm, key.KeyLength, key.KeyStatus,
                key.KeyStorageProvider, key.IsRolloverEnabled, key.RolloverPeriodSeconds,
                key.NextRolloverAction, key.NextRolloverTime)).ToArray())).ToArray(),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    private static string? Limit(string? value, int max)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, max)];
    }
    private static DnssecOperationModel Failure(string code, string message) => new(false, code, message);
}
