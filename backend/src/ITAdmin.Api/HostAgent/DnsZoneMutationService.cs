using System.Net;
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

public sealed partial class DnsZoneMutationService(
    AppDbContext context,
    ISecretProtector secretProtector,
    IHostAgentClient hostAgentClient,
    IDnsInventorySyncService inventorySyncService,
    ILogger<DnsZoneMutationService> logger) : IDnsZoneMutationService
{
    public async Task<DnsZoneMutationModel> ExecuteAsync(
        DnsZoneMutationCommand command, CancellationToken cancellationToken = default)
    {
        DnsZoneSnapshot? zone = null;
        DnsServer? server;
        if (command.Kind == DnsZoneMutationKind.Create)
        {
            server = await context.DnsServers.Include(x => x.CredentialProfile)
                .SingleOrDefaultAsync(x => x.Id == command.ServerId, cancellationToken);
        }
        else
        {
            if (command.ZoneSnapshotId is null) return Failure("ZoneRequired", "The inventory zone is required.");
            zone = await context.DnsZoneSnapshots
                .Include(x => x.InventorySnapshot).ThenInclude(x => x.DnsServer).ThenInclude(x => x.CredentialProfile)
                .SingleOrDefaultAsync(x => x.Id == command.ZoneSnapshotId, cancellationToken);
            if (zone is null || !zone.InventorySnapshot.IsActive)
                return Failure("SnapshotExpired", "The inventory snapshot is no longer active. Synchronize and retry.");
            server = zone.InventorySnapshot.DnsServer;
        }
        if (server is null) return Failure("ServerNotFound", "The DNS server was not found.");

        var settings = await context.DnsManagementSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.IsEnabled) return Failure("DnsManagementDisabled", "DNS Management is disabled.");
        if (!server.IsEnabled || !server.CredentialProfile.IsEnabled)
            return Failure("ServerDisabled", "The DNS server or its credential profile is disabled.");

        var name = Normalize(command.Name) ?? string.Empty;
        var zoneKind = command.ZoneKind;
        var isDsIntegrated = command.IsDsIntegrated;
        var dynamicUpdate = Normalize(command.DynamicUpdate);
        var replicationScope = Normalize(command.ReplicationScope);
        var directoryPartitionName = Normalize(command.DirectoryPartitionName);
        var zoneFile = Normalize(command.ZoneFile);
        var masterServers = command.MasterServers.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var forwarderTimeout = command.ForwarderTimeoutSeconds;
        var useRecursion = command.UseRecursion;
        string? expectedStateJson = null;

        if (zone is not null)
        {
            if (zone.VirtualizationInstance is not null)
                return Failure("VirtualizedZoneReadOnly", "Virtualization-instance zones are read-only in this version.");
            var properties = ReadProperties(zone.PropertiesJson);
            if (properties.IsAutoCreated || zone.Name is "." || zone.Name.Equals("TrustAnchors", StringComparison.OrdinalIgnoreCase))
                return Failure("ProtectedZone", "This system-managed DNS zone cannot be changed.");
            if (command.Kind == DnsZoneMutationKind.Delete && zone.IsSigned)
                return Failure("SignedZone", "Remove DNSSEC signing before deleting this zone.");
            if (!Enum.TryParse<DnsZoneKind>(zone.ZoneType, true, out zoneKind))
                return Failure("UnsupportedZoneType", "This DNS zone type is read-only in this version.");
            name = zone.Name;
            isDsIntegrated = zone.IsDsIntegrated;
            replicationScope = zone.ReplicationScope;
            directoryPartitionName = zone.DirectoryPartitionName;
            zoneFile = zone.ZoneFile;
            if (command.Kind == DnsZoneMutationKind.Delete)
            {
                dynamicUpdate = zone.DynamicUpdate;
                masterServers = properties.MasterServers.ToArray();
                forwarderTimeout = properties.ForwarderTimeoutSeconds;
                useRecursion = properties.UseRecursion;
            }
            expectedStateJson = JsonSerializer.Serialize(new
            {
                Name = zone.Name,
                ZoneType = zone.ZoneType,
                zone.IsReverseLookupZone,
                zone.IsDsIntegrated,
                zone.IsSigned,
                zone.IsPaused,
                IsAutoCreated = properties.IsAutoCreated,
                zone.DynamicUpdate,
                zone.ReplicationScope,
                zone.DirectoryPartitionName,
                zone.ZoneFile,
                MasterServers = properties.MasterServers,
                ForwarderTimeoutSeconds = properties.ForwarderTimeoutSeconds,
                UseRecursion = properties.UseRecursion,
            });
        }

        var validation = Validate(name, zoneKind, isDsIntegrated, dynamicUpdate, replicationScope,
            directoryPartitionName, zoneFile, masterServers, forwarderTimeout, useRecursion, command.Kind);
        if (validation is not null) return Failure("ValidationFailed", validation);

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
        HostAgentDnsZoneMutationResult result;
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.MutateDnsServerZone,
                CorrelationId = correlationId,
                DnsHostName = server.HostName,
                DnsPort = server.Port,
                DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                    ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
                DnsUserName = server.CredentialProfile.UserName,
                DnsPassword = password,
                DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
                DnsTimeoutSeconds = settings.CommandTimeoutSeconds,
                DnsZoneMutationKind = (HostAgentDnsZoneMutationKind)command.Kind,
                DnsZoneKind = (HostAgentDnsZoneKind)zoneKind,
                DnsZoneName = name,
                DnsZoneIsDsIntegrated = isDsIntegrated,
                DnsZoneDynamicUpdate = dynamicUpdate,
                DnsZoneReplicationScope = replicationScope,
                DnsZonePartitionName = directoryPartitionName,
                DnsZoneFile = zoneFile,
                DnsZoneMasterServers = masterServers,
                DnsZoneForwarderTimeoutSeconds = forwarderTimeout,
                DnsZoneUseRecursion = useRecursion,
                DnsExpectedZoneStateJson = expectedStateJson,
            }, cancellationToken);
            result = response.Status == HostAgentResponseStatus.Ok && response.DnsZoneMutation is not null
                ? response.DnsZoneMutation
                : new() { Success = false, FailureKind = "HostAgentRejected", Message = "The ITAdmin Host Agent rejected the DNS zone operation." };
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "Host Agent was unavailable for DNS zone operation on {DnsServerId}.", server.Id);
            result = new() { Success = false, FailureKind = "HostAgentUnavailable", Message = "The ITAdmin Host Agent is unavailable on the portal server." };
        }

        var before = result.Before is null ? null : MapZone(server, result.Before);
        var after = result.After is null ? null : MapZone(server, result.After);
        await WriteAuditAsync(server, zone, name, zoneKind, command, result, before, after,
            correlationId, cancellationToken);
        if (!result.Success) return new(false, result.FailureKind, result.Message, before, after, null);
        var sync = await inventorySyncService.EnqueuePostMutationAsync(server.Id, command.Actor, cancellationToken);
        return new(true, null, result.Message, before, after, sync.Value);
    }

    private async Task WriteAuditAsync(
        DnsServer server, DnsZoneSnapshot? zone, string name, DnsZoneKind zoneKind,
        DnsZoneMutationCommand command, HostAgentDnsZoneMutationResult result,
        DnsZoneInventoryModel? before, DnsZoneInventoryModel? after,
        string correlationId, CancellationToken cancellationToken)
    {
        context.AuditLogs.Add(new AuditLog
        {
            Action = $"DnsZone{command.Kind}",
            EntityName = "DnsZone",
            EntityId = zone?.Id.ToString(),
            Description = $"DNS zone {command.Kind.ToString().ToLowerInvariant()} for '{name}' on '{server.DisplayName}' {(result.Success ? "succeeded" : "failed")}.",
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
            OperationType = $"Zone{command.Kind}",
            Status = result.Success ? "Succeeded" : "Failed",
            ZoneName = name,
            RequestSummaryJson = JsonSerializer.Serialize(new
            {
                ZoneKind = zoneKind.ToString(),
                command.IsDsIntegrated,
                MasterServerCount = command.MasterServers.Count,
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

    private static DnsZoneInventoryModel MapZone(DnsServer server, HostAgentDnsZoneInventoryItem source) => new(
        Guid.Empty, Guid.Empty, server.Id, server.DisplayName, server.Environment,
        source.Name, source.ZoneType, source.IsReverseLookupZone, source.IsDsIntegrated,
        source.IsSigned, source.IsPaused, source.DynamicUpdate, source.ReplicationScope,
        source.DirectoryPartitionName, source.ZoneFile, source.VirtualizationInstance,
        source.ZoneScopes, source.IsAutoCreated, source.MasterServers,
        source.ForwarderTimeoutSeconds, source.UseRecursion, 0, DateTime.UtcNow);

    private static string? Validate(
        string name, DnsZoneKind kind, bool isDsIntegrated, string? dynamicUpdate,
        string? replicationScope, string? directoryPartitionName, string? zoneFile,
        IReadOnlyList<string> masterServers, int? forwarderTimeout, bool? useRecursion,
        DnsZoneMutationKind mutationKind)
    {
        if (name.Length is < 1 or > 253 || !ZoneNameRegex().IsMatch(name)) return "Enter a valid DNS zone name.";
        if (masterServers.Count > 16 || masterServers.Any(x => !IPAddress.TryParse(x, out _)))
            return "Master servers must contain at most 16 valid IP addresses.";
        if (kind == DnsZoneKind.Secondary && isDsIntegrated) return "Secondary zones cannot be Active Directory integrated.";
        if (kind is DnsZoneKind.Secondary or DnsZoneKind.Stub or DnsZoneKind.Forwarder
            && mutationKind != DnsZoneMutationKind.Delete && masterServers.Count == 0)
            return "At least one master server is required for this zone type.";
        if (!isDsIntegrated && kind != DnsZoneKind.Forwarder
            && (string.IsNullOrWhiteSpace(zoneFile) || zoneFile.Length > 255
                || !zoneFile.EndsWith(".dns", StringComparison.OrdinalIgnoreCase)
                || zoneFile.IndexOfAny(['/', '\\', ':']) >= 0))
            return "Enter a .dns zone file name without a path.";
        if (isDsIntegrated)
        {
            if (replicationScope is not ("Forest" or "Domain" or "Legacy" or "Custom"))
                return "Select an Active Directory replication scope.";
            if (replicationScope == "Custom" && string.IsNullOrWhiteSpace(directoryPartitionName))
                return "Directory partition name is required for custom replication.";
        }
        if (kind == DnsZoneKind.Primary)
        {
            if (dynamicUpdate is not ("None" or "NonsecureAndSecure" or "Secure"))
                return "Select a dynamic update policy.";
            if (!isDsIntegrated && dynamicUpdate == "Secure")
                return "Secure-only dynamic updates require an Active Directory-integrated zone.";
        }
        if (kind == DnsZoneKind.Forwarder
            && (forwarderTimeout is null or < 0 or > 15 || useRecursion is null))
            return "Conditional forwarder timeout and recursion settings are required.";
        return null;
    }

    private static ZoneProperties ReadProperties(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? "{}");
            var root = document.RootElement;
            var masters = root.TryGetProperty("masterServers", out var values) && values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray()
                : [];
            return new(
                root.TryGetProperty("isAutoCreated", out var auto) && auto.ValueKind is JsonValueKind.True or JsonValueKind.False && auto.GetBoolean(),
                masters,
                root.TryGetProperty("forwarderTimeoutSeconds", out var timeout) && timeout.TryGetInt32(out var timeoutValue) ? timeoutValue : null,
                root.TryGetProperty("useRecursion", out var recursion) && recursion.ValueKind is JsonValueKind.True or JsonValueKind.False ? recursion.GetBoolean() : null);
        }
        catch (JsonException)
        {
            return new(false, [], null, null);
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Limit(string? value, int maxLength)
    {
        var normalized = Normalize(value);
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
    private static DnsZoneMutationModel Failure(string code, string message) =>
        new(false, code, message, null, null, null);

    private sealed record ZoneProperties(
        bool IsAutoCreated, IReadOnlyList<string> MasterServers,
        int? ForwarderTimeoutSeconds, bool? UseRecursion);

    [GeneratedRegex(@"^(?=.{1,253}\.?$)[A-Za-z0-9_](?:[A-Za-z0-9_-]{0,61}[A-Za-z0-9_])?(?:\.[A-Za-z0-9_](?:[A-Za-z0-9_-]{0,61}[A-Za-z0-9_])?)*\.?$")]
    private static partial Regex ZoneNameRegex();
}
