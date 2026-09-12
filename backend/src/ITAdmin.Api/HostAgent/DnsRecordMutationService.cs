using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.HostAgent.Contracts;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Api.HostAgent;

public sealed partial class DnsRecordMutationService(
    AppDbContext context,
    ISecretProtector secretProtector,
    IHostAgentClient hostAgentClient,
    IDnsInventorySyncService inventorySyncService,
    ILogger<DnsRecordMutationService> logger) : IDnsRecordMutationService
{
    public async Task<DnsRecordMutationModel> ExecuteAsync(
        DnsRecordMutationCommand command, CancellationToken cancellationToken = default)
    {
        var zone = await context.DnsZoneSnapshots
            .Include(x => x.InventorySnapshot).ThenInclude(x => x.DnsServer).ThenInclude(x => x.CredentialProfile)
            .SingleOrDefaultAsync(x => x.Id == command.ZoneSnapshotId, cancellationToken);
        if (zone is null || !zone.InventorySnapshot.IsActive)
            return Failure("SnapshotExpired", "The inventory snapshot is no longer active. Synchronize and retry.");

        var server = zone.InventorySnapshot.DnsServer;
        var settings = await context.DnsManagementSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.IsEnabled) return Failure("DnsManagementDisabled", "DNS Management is disabled.");
        if (!server.IsEnabled || !server.CredentialProfile.IsEnabled)
            return Failure("ServerDisabled", "The DNS server or its credential profile is disabled.");

        DnsRecordSnapshot? cachedRecord = null;
        var relativeName = command.RelativeName.Trim();
        var recordType = command.RecordType.Trim().ToUpperInvariant();
        var zoneScope = NormalizeOptional(command.ZoneScope);
        var expectedHash = NormalizeOptional(command.ExpectedRecordHash);
        var ttl = command.TimeToLiveSeconds;

        if (command.Kind is DnsRecordMutationKind.Update or DnsRecordMutationKind.Delete)
        {
            if (command.RecordSnapshotId is null)
                return Failure("RecordRequired", "The inventory record is required.");
            cachedRecord = await context.DnsRecordSnapshots.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == command.RecordSnapshotId && x.DnsZoneSnapshotId == zone.Id,
                    cancellationToken);
            if (cachedRecord is null) return Failure("RecordNotFound", "The inventory record was not found.");
            if (!string.Equals(expectedHash, cachedRecord.RecordHash, StringComparison.OrdinalIgnoreCase))
                return Failure("RecordChanged", "The record has changed in the inventory. Reload and retry.");
            relativeName = cachedRecord.RelativeName;
            recordType = cachedRecord.RecordType;
            zoneScope = cachedRecord.ZoneScope;
            if (command.Kind == DnsRecordMutationKind.Delete) ttl = cachedRecord.TimeToLiveSeconds;
        }
        else if (!ScopeExists(zone.PropertiesJson, zoneScope))
        {
            return Failure("InvalidZoneScope", "The selected zone scope is not present in the active inventory.");
        }

        var validationError = Validate(relativeName, recordType, command.Values, ttl, command.Kind);
        if (validationError is not null) return Failure("ValidationFailed", validationError);

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
        HostAgentDnsRecordMutationResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.MutateDnsServerResourceRecord,
                CorrelationId = correlationId,
                DnsHostName = server.HostName,
                DnsPort = server.Port,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName,
                DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds,
                DnsRecordMutationKind = command.Kind switch
                {
                    DnsRecordMutationKind.Create => HostAgentDnsRecordMutationKind.Create,
                    DnsRecordMutationKind.Update => HostAgentDnsRecordMutationKind.Update,
                    _ => HostAgentDnsRecordMutationKind.Delete,
                },
                DnsZoneName = zone.Name,
                DnsZoneScope = zoneScope,
                DnsVirtualizationInstance = zone.VirtualizationInstance,
                DnsRecordRelativeName = relativeName,
                DnsRecordType = recordType,
                DnsRecordValues = command.Values,
                DnsRecordTimeToLiveSeconds = ttl,
                DnsExpectedRecordHash = expectedHash,
                DnsExpectedRecordDataJson = cachedRecord?.RecordDataJson,
                DnsExpectedRecordTimeToLiveSeconds = cachedRecord?.TimeToLiveSeconds,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnsRecordMutation is not null
                ? response.DnsRecordMutation
                : new HostAgentDnsRecordMutationResult
                {
                    Success = false,
                    FailureKind = "HostAgentRejected",
                    Message = "The ITAdmin Host Agent rejected the DNS record operation.",
                };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS record operation on {DnsServerId}.", server.Id);
            result = new HostAgentDnsRecordMutationResult
            {
                Success = false,
                FailureKind = "HostAgentUnavailable",
                Message = "The ITAdmin Host Agent is unavailable on the portal server.",
            };
        }

        var before = result.Before is null ? null : MapRecord(zone, result.Before);
        var after = result.After is null ? null : MapRecord(zone, result.After);
        await WriteAuditAsync(server, zone, relativeName, recordType, command, result,
            before, after, correlationId, cancellationToken);
        if (!result.Success) return new(false, result.FailureKind, result.Message, before, after, null);

        var sync = await inventorySyncService.EnqueuePostMutationAsync(server.Id, command.Actor, cancellationToken);
        return new(true, null, result.Message, before, after, sync.Value);
    }

    private async Task WriteAuditAsync(
        DnsServer server, DnsZoneSnapshot zone, string recordName, string recordType,
        DnsRecordMutationCommand command, HostAgentDnsRecordMutationResult result,
        DnsRecordInventoryModel? before, DnsRecordInventoryModel? after,
        string correlationId, CancellationToken cancellationToken)
    {
        var action = $"DnsRecord{command.Kind}";
        context.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = "DnsRecord",
            EntityId = command.RecordSnapshotId?.ToString(),
            Description = $"DNS record {command.Kind.ToString().ToLowerInvariant()} for '{recordName}.{zone.Name}' on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
            ActorUserId = command.Actor.UserId,
            ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64),
            UserAgent = Limit(command.Actor.UserAgent, 1024),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id,
            ServerDisplayName = server.DisplayName,
            OperationType = $"Record{command.Kind}",
            Status = result.Success ? "Succeeded" : "Failed",
            ZoneName = zone.Name,
            RecordName = recordName,
            RecordType = recordType,
            RequestSummaryJson = JsonSerializer.Serialize(new
            {
                command.TimeToLiveSeconds,
                command.ZoneScope,
                ValueCount = command.Values.Count,
            }),
            BeforeSnapshotJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterSnapshotJson = after is null ? null : JsonSerializer.Serialize(after),
            ErrorCode = Limit(result.FailureKind, 64),
            ErrorMessage = result.Success ? null : Limit(result.Message, 2000),
            ActorUserId = command.Actor.UserId,
            ActorUserName = Limit(command.Actor.UserName, 100),
            IpAddress = Limit(command.Actor.IpAddress, 64),
            UserAgent = Limit(command.Actor.UserAgent, 1024),
            CorrelationId = correlationId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static DnsRecordInventoryModel MapRecord(
        DnsZoneSnapshot zone, HostAgentDnsRecordInventoryItem source)
    {
        using var document = JsonDocument.Parse(source.RecordDataJson);
        var canonicalJson = JsonSerializer.Serialize(document.RootElement);
        var relativeName = source.RelativeName.Trim();
        var recordType = source.RecordType.Trim().ToUpperInvariant();
        var scope = NormalizeOptional(source.ZoneScope);
        var ttl = Math.Max(0, source.TimeToLiveSeconds);
        var fqdn = relativeName == "@" ? zone.Name.TrimEnd('.')
            : relativeName.EndsWith('.') ? relativeName.TrimEnd('.') : $"{relativeName}.{zone.Name.TrimEnd('.')}";
        var hashInput = string.Join('\n', zone.Name.ToLowerInvariant(), relativeName.ToLowerInvariant(),
            recordType, canonicalJson, ttl.ToString(CultureInfo.InvariantCulture),
            scope?.ToLowerInvariant() ?? "", zone.VirtualizationInstance?.ToLowerInvariant() ?? "");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant();
        return new(Guid.Empty, relativeName, fqdn, recordType, canonicalJson, canonicalJson,
            ttl, source.Timestamp, scope, zone.VirtualizationInstance, hash);
    }

    private static string? Validate(
        string name, string type, IReadOnlyList<string> values, int ttl, DnsRecordMutationKind kind)
    {
        if (name.Length is < 1 or > 253 || !OwnerNameRegex().IsMatch(name))
            return "Enter a valid relative DNS record name.";
        if (ttl is < 0 or > 2_147_483) return "TTL must be between 0 and 2147483 seconds.";
        if (type is not ("A" or "AAAA" or "CNAME" or "MX" or "NS" or "PTR" or "SRV" or "TXT"))
            return "This record type is read-only in this version.";
        if (kind == DnsRecordMutationKind.Delete) return null;
        if (values.Any(x => x is null || x.Length is < 1 or > 2048 || x.Any(char.IsControl)))
            return "Record values are required and must not contain control characters.";
        return type switch
        {
            "A" when values.Count != 1 || !IsAddress(values[0], AddressFamily.InterNetwork) => "Enter one valid IPv4 address.",
            "AAAA" when values.Count != 1 || !IsAddress(values[0], AddressFamily.InterNetworkV6) => "Enter one valid IPv6 address.",
            "CNAME" or "NS" or "PTR" when values.Count != 1 || !TargetNameRegex().IsMatch(values[0]) => "Enter one valid DNS target name.",
            "MX" when values.Count != 2 || !IsUInt16(values[0]) || !TargetNameRegex().IsMatch(values[1]) => "Enter an MX preference and a valid mail server name.",
            "SRV" when values.Count != 4 || !values.Take(3).All(IsUInt16) || !TargetNameRegex().IsMatch(values[3]) => "Enter SRV priority, weight, port, and a valid target name.",
            "TXT" when values.Count != 1 => "Enter one TXT value.",
            _ => null,
        };
    }

    private static bool IsAddress(string value, AddressFamily family) =>
        IPAddress.TryParse(value, out var address) && address.AddressFamily == family;
    private static bool IsUInt16(string value) => ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    private static bool ScopeExists(string? propertiesJson, string? scope)
    {
        if (scope is null) return true;
        try
        {
            using var document = JsonDocument.Parse(propertiesJson ?? "{}");
            return document.RootElement.TryGetProperty("zoneScopes", out var scopes)
                && scopes.EnumerateArray().Any(x => string.Equals(x.GetString(), scope, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Limit(string? value, int maxLength)
    {
        var normalized = NormalizeOptional(value);
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
    private static DnsRecordMutationModel Failure(string code, string message) =>
        new(false, code, message, null, null, null);

    [GeneratedRegex(@"^(?:@|\*|[A-Za-z0-9_*-](?:[A-Za-z0-9_.*-]{0,251}[A-Za-z0-9_*-])?)$")]
    private static partial Regex OwnerNameRegex();
    [GeneratedRegex(@"^(?:\.|[A-Za-z0-9_-](?:[A-Za-z0-9_.-]{0,251}[A-Za-z0-9_-])?\.?)$")]
    private static partial Regex TargetNameRegex();
}
