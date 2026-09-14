using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Persistence.Services;

public sealed class DnsManagementAdministrationService(AppDbContext context, ISecretProtector secretProtector)
    : IDnsManagementAdministrationService
{
    public async Task<DnsManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var entity = await context.DnsManagementSettings.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return MapSettings(entity ?? new DnsManagementSettings());
    }

    public async Task<DnsAdministrationResult<DnsManagementSettingsModel>> UpdateSettingsAsync(
        UpdateDnsManagementSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var error = ValidateSettings(request);
        if (error is not null) return new(false, error);

        var entity = await context.DnsManagementSettings
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        entity ??= new DnsManagementSettings { CreatedAt = now, CreatedBy = ActorName(request.Actor) };
        entity.IsEnabled = request.IsEnabled;
        entity.AutomaticSyncEnabled = request.AutomaticSyncEnabled;
        entity.DefaultSyncIntervalMinutes = request.DefaultSyncIntervalMinutes;
        entity.HealthCheckIntervalMinutes = request.HealthCheckIntervalMinutes;
        entity.CommandTimeoutSeconds = request.CommandTimeoutSeconds;
        entity.MaxParallelServers = request.MaxParallelServers;
        entity.SnapshotRetentionDays = request.SnapshotRetentionDays;
        entity.SyncRecordInventory = request.SyncRecordInventory;
        entity.PromptForFullSyncOnComparisonOpen = request.PromptForFullSyncOnComparisonOpen;
        entity.ComparisonSnapshotStaleAfterMinutes = request.ComparisonSnapshotStaleAfterMinutes;
        entity.UpdatedAt = now;
        entity.UpdatedBy = ActorName(request.Actor);
        if (context.Entry(entity).State == EntityState.Detached) context.DnsManagementSettings.Add(entity);
        AddAudit("Update", "DnsManagementSettings", entity.Id, "DNS management settings updated.", request.Actor);
        await context.SaveChangesAsync(cancellationToken);
        return new(true, "DNS management settings updated.", MapSettings(entity));
    }

    public async Task<IReadOnlyList<DnsCredentialProfileModel>> GetCredentialProfilesAsync(CancellationToken cancellationToken = default) =>
        await context.DnsCredentialProfiles.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new DnsCredentialProfileModel(x.Id, x.Name, x.AuthenticationMode, x.UserName,
                x.EncryptedPassword != "", x.IsEnabled, x.LastValidatedAt,
                x.LastValidationStatus, x.LastValidationMessage)).ToListAsync(cancellationToken);

    public async Task<DnsAdministrationResult<DnsCredentialProfileModel>> SaveCredentialProfileAsync(
        SaveDnsCredentialProfileRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var userName = request.UserName.Trim();
        if (name.Length is < 1 or > 150) return new(false, "Credential profile name is required and may contain at most 150 characters.");
        if (userName.Length is < 1 or > 256) return new(false, "User name is required and may contain at most 256 characters.");
        if (request.Password?.Length > 2048) return new(false, "Password is too long.");
        if (await context.DnsCredentialProfiles.AnyAsync(
                x => x.Id != request.Id && x.Name.ToLower() == name.ToLower(), cancellationToken))
            return new(false, "A credential profile with this name already exists.");

        DnsCredentialProfile entity;
        var now = DateTime.UtcNow;
        if (request.Id is { } id)
        {
            entity = await context.DnsCredentialProfiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? null!;
            if (entity is null) return new(false, "Credential profile was not found.");
            if (string.IsNullOrEmpty(request.Password) && string.IsNullOrEmpty(entity.EncryptedPassword))
                return new(false, "Password is required.");
            entity.UpdatedAt = now;
            entity.UpdatedBy = ActorName(request.Actor);
            if (request.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
                && await context.DnsServers.AnyAsync(
                    x => x.DnsCredentialProfileId == id && x.Transport == DnsConnectionTransport.Http,
                    cancellationToken))
                return new(false, "This profile is assigned to a WinRM HTTP server. Basic authentication requires HTTPS.");
        }
        else
        {
            if (string.IsNullOrEmpty(request.Password)) return new(false, "Password is required.");
            entity = new DnsCredentialProfile { CreatedAt = now, CreatedBy = ActorName(request.Actor) };
            context.DnsCredentialProfiles.Add(entity);
        }

        entity.Name = name;
        entity.AuthenticationMode = request.AuthenticationMode;
        entity.UserName = userName;
        entity.IsEnabled = request.IsEnabled;
        if (!string.IsNullOrEmpty(request.Password)) entity.EncryptedPassword = secretProtector.Protect(request.Password);
        AddAudit(request.Id is null ? "Create" : "Update", "DnsCredentialProfile", entity.Id,
            $"DNS credential profile '{name}' saved; secret value was not logged.", request.Actor);
        await context.SaveChangesAsync(cancellationToken);
        return new(true, "Credential profile saved.", MapCredential(entity));
    }

    public async Task<DnsAdministrationResult<bool>> DeleteCredentialProfileAsync(
        Guid id, DnsActorContext actor, CancellationToken cancellationToken = default)
    {
        var entity = await context.DnsCredentialProfiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return new(false, "Credential profile was not found.");
        if (await context.DnsServers.AnyAsync(x => x.DnsCredentialProfileId == id, cancellationToken))
            return new(false, "Credential profile is assigned to a DNS server and cannot be deleted.");
        context.DnsCredentialProfiles.Remove(entity);
        AddAudit("Delete", "DnsCredentialProfile", entity.Id,
            $"DNS credential profile '{entity.Name}' deleted; secret value was not logged.", actor);
        await context.SaveChangesAsync(cancellationToken);
        return new(true, "Credential profile deleted.", true);
    }

    public async Task<IReadOnlyList<DnsServerModel>> GetServersAsync(CancellationToken cancellationToken = default) =>
        await context.DnsServers.AsNoTracking().OrderBy(x => x.DisplayName)
            .Select(x => new DnsServerModel(x.Id, x.DisplayName, x.HostName, x.Port, x.Transport, x.Environment,
                x.DnsCredentialProfileId, x.CredentialProfile.Name, x.IsEnabled, x.SyncIntervalMinutes,
                x.TlsCertificateThumbprint, x.Notes, x.OperatingSystemVersion, x.DnsServerVersion,
                x.LastSeenAt, x.LastSuccessfulSyncAt, x.LastSyncStatus, x.LastSyncMessage))
            .ToListAsync(cancellationToken);

    public async Task<DnsAdministrationResult<DnsServerModel>> SaveServerAsync(
        SaveDnsServerRequest request, CancellationToken cancellationToken = default)
    {
        var displayName = request.DisplayName.Trim();
        var hostName = request.HostName.Trim().TrimEnd('.').ToLowerInvariant();
        if (displayName.Length is < 1 or > 150) return new(false, "Display name is required and may contain at most 150 characters.");
        if (hostName.Length is < 1 or > 253 || Uri.CheckHostName(hostName) == UriHostNameType.Unknown)
            return new(false, "A valid DNS server host name or IP address is required.");
        if (request.Port is < 1 or > 65535) return new(false, "Port must be between 1 and 65535.");
        if (!Enum.IsDefined(request.Transport)) return new(false, "Connection transport is invalid.");
        if (request.SyncIntervalMinutes is < 1 or > 1440) return new(false, "Sync interval must be between 1 and 1440 minutes.");
        if (request.Notes?.Trim().Length > 2000) return new(false, "Notes may contain at most 2000 characters.");
        if (request.Transport == DnsConnectionTransport.Http && !string.IsNullOrWhiteSpace(request.TlsCertificateThumbprint))
            return new(false, "A certificate thumbprint cannot be used with WinRM HTTP.");
        if (request.TlsCertificateThumbprint?.Any(x => !Uri.IsHexDigit(x) && !char.IsWhiteSpace(x) && x is not ':' and not '-') == true)
            return new(false, "Certificate thumbprint contains invalid characters.");
        var thumbprint = NormalizeThumbprint(request.TlsCertificateThumbprint);
        if (thumbprint is not null && (thumbprint.Length is not (40 or 64) || thumbprint.Any(x => !Uri.IsHexDigit(x))))
            return new(false, "Certificate thumbprint must be a SHA-1 or SHA-256 hexadecimal value.");
        var authenticationMode = await context.DnsCredentialProfiles
            .Where(x => x.Id == request.CredentialProfileId && x.IsEnabled)
            .Select(x => (DnsAuthenticationMode?)x.AuthenticationMode)
            .SingleOrDefaultAsync(cancellationToken);
        if (authenticationMode is null)
            return new(false, "An active credential profile is required.");
        if (request.Transport == DnsConnectionTransport.Http && authenticationMode == DnsAuthenticationMode.BasicOverTls)
            return new(false, "Basic authentication is not allowed over WinRM HTTP. Use a Negotiate credential profile.");
        if (await context.DnsServers.AnyAsync(x => x.Id != request.Id && x.DisplayName.ToLower() == displayName.ToLower(), cancellationToken))
            return new(false, "A DNS server with this display name already exists.");
        if (await context.DnsServers.AnyAsync(x => x.Id != request.Id && x.HostName.ToLower() == hostName && x.Port == request.Port, cancellationToken))
            return new(false, "This DNS server endpoint is already registered.");

        DnsServer entity;
        var now = DateTime.UtcNow;
        if (request.Id is { } id)
        {
            entity = await context.DnsServers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? null!;
            if (entity is null) return new(false, "DNS server was not found.");
            entity.UpdatedAt = now;
            entity.UpdatedBy = ActorName(request.Actor);
        }
        else
        {
            entity = new DnsServer { CreatedAt = now, CreatedBy = ActorName(request.Actor) };
            context.DnsServers.Add(entity);
        }
        entity.DisplayName = displayName;
        entity.HostName = hostName;
        entity.Port = request.Port;
        entity.Transport = request.Transport;
        entity.Environment = request.Environment;
        entity.DnsCredentialProfileId = request.CredentialProfileId;
        entity.IsEnabled = request.IsEnabled;
        entity.SyncIntervalMinutes = request.SyncIntervalMinutes;
        entity.TlsCertificateThumbprint = thumbprint;
        entity.Notes = Normalize(request.Notes, 2000);
        AddAudit(request.Id is null ? "Create" : "Update", "DnsServer", entity.Id,
            $"DNS server '{displayName}' ({request.Transport.ToString().ToUpperInvariant()} {hostName}:{request.Port}) saved.", request.Actor);
        await context.SaveChangesAsync(cancellationToken);
        return new(true, "DNS server saved.", MapServer(entity, await context.DnsCredentialProfiles.AsNoTracking()
            .Where(x => x.Id == entity.DnsCredentialProfileId).Select(x => x.Name).SingleAsync(cancellationToken)));
    }

    private static string? ValidateSettings(UpdateDnsManagementSettingsRequest x) =>
        x.DefaultSyncIntervalMinutes is < 1 or > 1440 ? "Default sync interval must be between 1 and 1440 minutes." :
        x.HealthCheckIntervalMinutes is < 1 or > 1440 ? "Health check interval must be between 1 and 1440 minutes." :
        x.CommandTimeoutSeconds is < 5 or > 300 ? "Command timeout must be between 5 and 300 seconds." :
        x.MaxParallelServers is < 1 or > 20 ? "Maximum parallel servers must be between 1 and 20." :
        x.SnapshotRetentionDays is < 1 or > 3650 ? "Snapshot retention must be between 1 and 3650 days." :
        x.ComparisonSnapshotStaleAfterMinutes is < 1 or > 10080 ? "Comparison stale threshold must be between 1 and 10080 minutes." : null;

    private void AddAudit(string action, string entityName, Guid id, string description, DnsActorContext actor) =>
        context.AuditLogs.Add(new AuditLog { Action = action, EntityName = entityName, EntityId = id.ToString(), Description = description,
            ActorUserId = actor.UserId, ActorUserName = actor.UserName, IpAddress = actor.IpAddress, UserAgent = actor.UserAgent, CreatedAt = DateTimeOffset.UtcNow });
    private static string ActorName(DnsActorContext actor) => string.IsNullOrWhiteSpace(actor.UserName) ? "system" : actor.UserName.Trim();
    private static string? Normalize(string? value, int max) { var result = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); return result is { Length: > 0 } && result.Length <= max ? result : result?[..max]; }
    private static string? NormalizeThumbprint(string? value) => string.IsNullOrWhiteSpace(value) ? null : string.Concat(value.Where(Uri.IsHexDigit)).ToUpperInvariant();
    private static DnsManagementSettingsModel MapSettings(DnsManagementSettings x) => new(x.IsEnabled, x.AutomaticSyncEnabled, x.DefaultSyncIntervalMinutes, x.HealthCheckIntervalMinutes, x.CommandTimeoutSeconds, x.MaxParallelServers, x.SnapshotRetentionDays, x.SyncRecordInventory, x.PromptForFullSyncOnComparisonOpen, x.ComparisonSnapshotStaleAfterMinutes, x.UpdatedAt, x.UpdatedBy);
    private static DnsCredentialProfileModel MapCredential(DnsCredentialProfile x) => new(x.Id, x.Name, x.AuthenticationMode, x.UserName, !string.IsNullOrEmpty(x.EncryptedPassword), x.IsEnabled, x.LastValidatedAt, x.LastValidationStatus, x.LastValidationMessage);
    private static DnsServerModel MapServer(DnsServer x, string credentialProfileName) => new(x.Id, x.DisplayName, x.HostName, x.Port, x.Transport, x.Environment, x.DnsCredentialProfileId, credentialProfileName, x.IsEnabled, x.SyncIntervalMinutes, x.TlsCertificateThumbprint, x.Notes, x.OperatingSystemVersion, x.DnsServerVersion, x.LastSeenAt, x.LastSuccessfulSyncAt, x.LastSyncStatus, x.LastSyncMessage);
}
