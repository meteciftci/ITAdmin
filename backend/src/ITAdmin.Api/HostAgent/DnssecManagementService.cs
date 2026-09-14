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
                DnsUseSsl = server.Transport == DnsConnectionTransport.Https,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName, DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds, DnssecAction = action,
                DnssecZoneName = command?.ZoneName?.Trim(), DnssecKeyIds = command?.KeyIds,
                DnssecValidationEnabled = command?.ValidationEnabled,
                DnssecTrustPointName = command?.TrustPointName?.Trim(),
                DnssecTrustAnchorType = command?.TrustAnchorType,
                DnssecCryptoAlgorithm = command?.CryptoAlgorithm, DnssecKeyTag = command?.KeyTag,
                DnssecDigestType = command?.DigestType, DnssecDigest = command?.Digest?.Trim(),
                DnssecBase64Data = command?.Base64Data?.Trim(),
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
            DnssecAction.RolloverKeys => "DnssecKeyRollover",
            DnssecAction.SetValidationEnabled => "DnssecValidationUpdate",
            DnssecAction.RetrieveRootTrustAnchor => "DnssecRootTrustAnchorRetrieve",
            DnssecAction.AddDsTrustAnchor => "DnssecDsTrustAnchorAdd",
            DnssecAction.AddDnsKeyTrustAnchor => "DnssecDnsKeyTrustAnchorAdd",
            _ => "DnssecTrustAnchorRemove",
        };
        context.AuditLogs.Add(new AuditLog
        {
            Action = operation, EntityName = "DnsServer", EntityId = server.Id.ToString(),
            Description = $"DNSSEC {command.Action} '{command.ZoneName ?? command.TrustPointName ?? "resolver"}' on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
            ActorUserId = command.Actor.UserId, ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64), UserAgent = Limit(command.Actor.UserAgent, 1024), CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id, ServerDisplayName = server.DisplayName, OperationType = operation,
            Status = result.Success ? "Succeeded" : "Failed", ZoneName = Limit(command.ZoneName ?? command.TrustPointName, 253),
            RequestSummaryJson = JsonSerializer.Serialize(new { command.Action, command.ZoneName, command.KeyIds, command.ValidationEnabled,
                command.TrustPointName, command.TrustAnchorType, command.CryptoAlgorithm, command.KeyTag, command.DigestType }),
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
        if (string.IsNullOrWhiteSpace(value.ExpectedStateToken) || value.ExpectedStateToken.Length > 800_000)
            return "Refresh the live DNSSEC configuration and retry.";
        if ((value.Action is DnssecAction.SignWithDefaults or DnssecAction.Resign or DnssecAction.Unsign or DnssecAction.RolloverKeys)
            && (string.IsNullOrWhiteSpace(value.ZoneName) || value.ZoneName.Length > 253 || value.ZoneName.Any(char.IsControl)))
            return "A valid DNS zone name is required.";
        if (value.Action == DnssecAction.RolloverKeys
            && (value.KeyIds.Count is < 1 or > 8 || value.KeyIds.Any(x => x == Guid.Empty)
                || value.KeyIds.Distinct().Count() != value.KeyIds.Count))
            return "Select between one and eight unique signing keys.";
        if (value.Action != DnssecAction.RolloverKeys && value.KeyIds.Count > 0)
            return "Signing keys are only accepted for rollover.";
        if (value.Action == DnssecAction.SetValidationEnabled && value.ValidationEnabled is null)
            return "The DNSSEC validation state is required.";
        if ((value.Action is DnssecAction.AddDsTrustAnchor or DnssecAction.AddDnsKeyTrustAnchor or DnssecAction.RemoveTrustAnchorType)
            && (string.IsNullOrWhiteSpace(value.TrustPointName) || value.TrustPointName.Length > 253 || value.TrustPointName.Any(char.IsControl)))
            return "A valid trust point name is required.";
        string[] algorithms = ["RsaSha1", "RsaSha256", "RsaSha512", "RsaSha1NSec3", "ECDsaP256Sha256", "ECDsaP384Sha384"];
        if ((value.Action is DnssecAction.AddDsTrustAnchor or DnssecAction.AddDnsKeyTrustAnchor)
            && !algorithms.Contains(value.CryptoAlgorithm, StringComparer.Ordinal))
            return "The DNSSEC crypto algorithm is not supported.";
        if (value.Action == DnssecAction.AddDsTrustAnchor)
        {
            if (value.KeyTag is null or < 0 or > 65535) return "The DS key tag must be between 0 and 65535.";
            var length = value.DigestType switch { "Sha1" => 40, "Sha256" => 64, "Sha384" => 96, _ => 0 };
            if (length == 0 || value.Digest?.Length != length || value.Digest.Any(x => !Uri.IsHexDigit(x)))
                return "The DS digest must match the selected digest type.";
        }
        if (value.Action == DnssecAction.AddDnsKeyTrustAnchor)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value.Base64Data) || value.Base64Data.Length > 16_384
                    || Convert.FromBase64String(value.Base64Data).Length is < 1 or > 12_000)
                    return "A bounded base64 DNSKEY value is required.";
            }
            catch (FormatException) { return "A valid base64 DNSKEY value is required."; }
        }
        if (value.Action == DnssecAction.RemoveTrustAnchorType && value.TrustAnchorType is not ("DnsKey" or "Ds"))
            return "Select the trust anchor type to remove.";
        return null;
    }

    private static HostAgentDnssecAction Map(DnssecAction value) => value switch
    {
        DnssecAction.SignWithDefaults => HostAgentDnssecAction.SignWithDefaults,
        DnssecAction.Resign => HostAgentDnssecAction.Resign,
        DnssecAction.Unsign => HostAgentDnssecAction.Unsign,
        DnssecAction.RolloverKeys => HostAgentDnssecAction.RolloverKeys,
        DnssecAction.SetValidationEnabled => HostAgentDnssecAction.SetValidationEnabled,
        DnssecAction.RetrieveRootTrustAnchor => HostAgentDnssecAction.RetrieveRootTrustAnchor,
        DnssecAction.AddDsTrustAnchor => HostAgentDnssecAction.AddDsTrustAnchor,
        DnssecAction.AddDnsKeyTrustAnchor => HostAgentDnssecAction.AddDnsKeyTrustAnchor,
        _ => HostAgentDnssecAction.RemoveTrustAnchorType,
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
            new DnssecResolverConfigurationModel(value.Resolver.ValidationEnabled,
                value.Resolver.IsReadOnlyDomainController, value.Resolver.DirectoryServicesAvailable,
                value.Resolver.RootTrustAnchorsUrl, value.Resolver.TrustPoints.Select(point => new DnssecTrustPointModel(
                    point.Name, point.State, point.LastActiveRefreshTime, point.NextActiveRefreshTime,
                    point.Anchors.Select(anchor => new DnssecTrustAnchorModel(anchor.Type, anchor.State, anchor.Data)).ToArray())).ToArray()),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    private static string? Limit(string? value, int max)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, max)];
    }
    private static DnssecOperationModel Failure(string code, string message) => new(false, code, message);
}
