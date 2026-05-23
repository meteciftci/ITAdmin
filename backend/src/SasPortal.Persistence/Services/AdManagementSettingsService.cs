using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Audit;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class AdManagementSettingsService(
    AppDbContext context,
    ISecretProtector secretProtector,
    IAdOperationLogService adOperationLogService,
    IAdManagementValidationService validationService) : IAdManagementSettingsService
{
    private const int AuditDescriptionMaxLength = 2000;
    private const int AuditIpAddressMaxLength = 64;
    private const int AuditUserAgentMaxLength = 1024;
    private const int LdapPortMin = 1;
    private const int LdapPortMax = 65535;
    private const int PowerShellTimeoutMin = 5;
    private const int PowerShellTimeoutMax = 300;
    private const string MissingRequiredFieldsMessage =
        "AD yönetim ayarları için zorunlu alanlar eksik.";

    public async Task<AdManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var entity = await context.AdManagementSettings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return MapToModel(entity);
    }

    public async Task<UpdateAdManagementSettingsResult> UpdateSettingsAsync(
        UpdateAdManagementSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateUpdateRequest(request, out var validationMessage))
        {
            return new UpdateAdManagementSettingsResult(false, validationMessage, null);
        }

        var notificationSettings = request.NotificationSettings
            ?? AdManagementNotificationSettingsSerializer.CreateDefault();
        var notificationValidationError = AdManagementNotificationSettingsValidator.Validate(notificationSettings);
        if (notificationValidationError is not null)
        {
            return new UpdateAdManagementSettingsResult(false, notificationValidationError, null);
        }

        var entity = await context.AdManagementSettings
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var actor = request.ActorUserName ?? "system";

        var preferredControllers = NormalizePreferredDomainControllers(request.PreferredDomainControllers);
        var preferredControllersJson = preferredControllers.Count == 0
            ? null
            : JsonSerializer.Serialize(preferredControllers);

        var domainFqdn = NormalizeNullable(request.DomainFqdn);
        var netbios = NormalizeNullable(request.NetbiosDomainName);
        var defaultNc = NormalizeNullable(request.DefaultNamingContext);
        var baseDn = NormalizeNullable(request.BaseDn);
        var usersRootOu = NormalizeNullable(request.UsersRootOu);
        var disabledUsersOu = NormalizeNullable(request.DisabledUsersOu);
        var groupsSearchBase = NormalizeNullable(request.GroupsSearchBase);
        var computersSearchBase = NormalizeNullable(request.ComputersSearchBase);
        var serviceAccountUserName = NormalizeNullable(request.ServiceAccountUserName);

        var providedPassword = NormalizeNullable(request.ServiceAccountPassword);
        var hadPassword = !string.IsNullOrWhiteSpace(entity?.EncryptedServiceAccountPassword);

        if (request.IsEnabled)
        {
            if (string.IsNullOrWhiteSpace(domainFqdn) ||
                string.IsNullOrWhiteSpace(netbios) ||
                string.IsNullOrWhiteSpace(defaultNc) ||
                string.IsNullOrWhiteSpace(baseDn) ||
                string.IsNullOrWhiteSpace(usersRootOu) ||
                string.IsNullOrWhiteSpace(disabledUsersOu) ||
                string.IsNullOrWhiteSpace(serviceAccountUserName))
            {
                return new UpdateAdManagementSettingsResult(
                    false,
                    MissingRequiredFieldsMessage,
                    null);
            }

            var willHavePassword = !request.ClearServiceAccountPassword
                && (providedPassword is not null || hadPassword);
            if (!willHavePassword)
            {
                return new UpdateAdManagementSettingsResult(
                    false,
                    "Service account password is required when AD management is enabled.",
                    null);
            }
        }

        AdManagementValidationResult? validationResult = null;
        if (request.IsEnabled)
        {
            var effectivePassword = ResolveCandidateServiceAccountPassword(
                providedPassword,
                entity?.EncryptedServiceAccountPassword,
                request.ClearServiceAccountPassword);

            var candidate = new AdManagementConnectionParameters(
                DomainFqdn: domainFqdn,
                NetbiosDomainName: netbios,
                DefaultNamingContext: defaultNc,
                BaseDn: baseDn,
                UsersRootOu: usersRootOu,
                DisabledUsersOu: disabledUsersOu,
                GroupsSearchBase: groupsSearchBase,
                ComputersSearchBase: computersSearchBase,
                PreferredDomainControllers: preferredControllers,
                UseSsl: request.UseSsl,
                LdapPort: request.LdapPort,
                ServiceAccountUserName: serviceAccountUserName,
                ServiceAccountPassword: effectivePassword);

            var validationRequest = new AdManagementValidationRequest(
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent);

            validationResult = await validationService.ValidateConnectionAsync(
                candidate,
                validationRequest,
                cancellationToken);

            if (!validationResult.IsValid)
            {
                await WriteValidationLogsAsync(
                    validationResult,
                    validationRequest,
                    ResolvePrimaryDomainController(candidate),
                    settingsEntity: null,
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);

                return new UpdateAdManagementSettingsResult(
                    false,
                    validationResult.Message,
                    null,
                    validationResult);
            }
        }

        var isNew = entity is null;
        var beforeNotificationSettings = entity is null
            ? AdManagementNotificationSettingsSerializer.CreateDefault()
            : AdManagementNotificationSettingsSerializer.Deserialize(entity.NotificationSettingsJson);

        entity ??= new AdManagementSettings
        {
            CreatedAt = now,
            CreatedBy = actor
        };

        entity.IsEnabled = request.IsEnabled;
        entity.DomainFqdn = domainFqdn;
        entity.DefaultUserCreationUpnSuffix = NormalizeDefaultUserCreationUpnSuffix(
            request.DefaultUserCreationUpnSuffix);
        entity.NetbiosDomainName = netbios;
        entity.DefaultNamingContext = defaultNc;
        entity.BaseDn = baseDn;
        entity.UsersRootOu = usersRootOu;
        entity.DisabledUsersOu = disabledUsersOu;
        entity.GroupsSearchBase = groupsSearchBase;
        entity.ComputersSearchBase = computersSearchBase;
        entity.PreferredDomainControllersJson = preferredControllersJson;
        entity.UseSsl = request.UseSsl;
        entity.LdapPort = request.LdapPort;
        entity.ServiceAccountUserName = serviceAccountUserName;
        entity.PowerShellHealthEnabled = request.PowerShellHealthEnabled;
        entity.PowerShellTimeoutSeconds = request.PowerShellTimeoutSeconds;
        entity.NotificationSettingsJson = AdManagementNotificationSettingsSerializer.Serialize(notificationSettings);

        var passwordChanged = false;
        if (request.ClearServiceAccountPassword)
        {
            if (hadPassword)
            {
                passwordChanged = true;
            }

            entity.EncryptedServiceAccountPassword = null;
        }
        else if (!string.IsNullOrWhiteSpace(providedPassword))
        {
            entity.EncryptedServiceAccountPassword = secretProtector.Protect(providedPassword);
            passwordChanged = true;
        }

        if (isNew)
        {
            await context.AdManagementSettings.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.UpdatedAt = now;
            entity.UpdatedBy = actor;
        }

        if (validationResult is not null)
        {
            var validationRequestForLog = new AdManagementValidationRequest(
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent);

            await WriteValidationLogsAsync(
                validationResult,
                validationRequestForLog,
                primaryDomainController: ResolvePrimaryHostFromPreferred(preferredControllers, domainFqdn),
                settingsEntity: entity,
                cancellationToken);
        }

        var notificationAuditChanges = BuildNotificationSettingsAuditChanges(
            beforeNotificationSettings,
            notificationSettings);
        var notificationRulesSummary = AdManagementNotificationSettingsAuditBuilder.BuildRulesChangeSummary(
            beforeNotificationSettings,
            notificationSettings);
        var auditPrefix = string.IsNullOrWhiteSpace(notificationRulesSummary)
            ? $"AD management settings updated. Enabled: {entity.IsEnabled}. Password changed: {passwordChanged}."
            : $"AD management settings updated. Enabled: {entity.IsEnabled}. Password changed: {passwordChanged}. {notificationRulesSummary.TrimEnd('.', ' ')}.";
        var auditDescription = TruncateAuditDescription(
            AuditChangeSummaryBuilder.BuildUpdateDescription(
                auditPrefix,
                notificationAuditChanges));

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "AdManagementSettings",
                EntityId = AdManagementTargetObjectTypes.AdManagementSettings,
                Description = auditDescription,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);

        var summaryJson = BuildSettingsRequestSummary(request, entity, preferredControllers, passwordChanged);
        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.SettingsUpdated,
                Status = AdManagementOperationStatuses.Succeeded,
                TargetObjectType = AdManagementTargetObjectTypes.AdManagementSettings,
                RequestSummaryJson = summaryJson,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        var fresh = await GetSettingsAsync(cancellationToken);
        return new UpdateAdManagementSettingsResult(
            true,
            "AD management settings updated.",
            fresh,
            validationResult);
    }

    private string? ResolveCandidateServiceAccountPassword(
        string? providedPassword,
        string? encryptedStoredPassword,
        bool clearServiceAccountPassword)
    {
        if (clearServiceAccountPassword)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(providedPassword))
        {
            return providedPassword;
        }

        if (string.IsNullOrWhiteSpace(encryptedStoredPassword))
        {
            return null;
        }

        try
        {
            return secretProtector.Unprotect(encryptedStoredPassword);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolvePrimaryDomainController(AdManagementConnectionParameters candidate)
    {
        if (candidate.PreferredDomainControllers.Count > 0
            && !string.IsNullOrWhiteSpace(candidate.PreferredDomainControllers[0]))
        {
            return candidate.PreferredDomainControllers[0];
        }

        return string.IsNullOrWhiteSpace(candidate.DomainFqdn) ? null : candidate.DomainFqdn;
    }

    private static string? ResolvePrimaryHostFromPreferred(
        IReadOnlyList<string> preferredControllers,
        string? domainFqdn)
    {
        if (preferredControllers.Count > 0 && !string.IsNullOrWhiteSpace(preferredControllers[0]))
        {
            return preferredControllers[0];
        }

        return string.IsNullOrWhiteSpace(domainFqdn) ? null : domainFqdn;
    }

    private async Task WriteValidationLogsAsync(
        AdManagementValidationResult result,
        AdManagementValidationRequest request,
        string? primaryDomainController,
        AdManagementSettings? settingsEntity,
        CancellationToken cancellationToken)
    {
        if (settingsEntity is not null)
        {
            settingsEntity.LastValidatedAt = result.CheckedAt.UtcDateTime;
            settingsEntity.LastValidationStatus = result.IsValid
                ? AdManagementValidationStatuses.Ok
                : AdManagementValidationStatuses.Failed;
            settingsEntity.LastValidationMessage = string.IsNullOrWhiteSpace(result.Message)
                ? null
                : (result.Message.Length > 2000 ? result.Message[..2000] : result.Message);
        }

        var safeMessage = SanitizeForLog(result.Message);
        var auditDescription = TruncateAuditDescription(
            result.IsValid
                ? "AD management settings validation succeeded."
                : $"AD management settings validation failed: {safeMessage}.");

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Validate",
                EntityName = "AdManagementSettings",
                EntityId = AdManagementTargetObjectTypes.AdManagementSettings,
                Description = auditDescription,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = result.CheckedAt
            },
            cancellationToken);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.SettingsValidated,
                Status = result.IsValid
                    ? AdManagementOperationStatuses.Succeeded
                    : AdManagementOperationStatuses.Failed,
                TargetObjectType = AdManagementTargetObjectTypes.AdManagementSettings,
                ErrorMessage = result.IsValid ? null : safeMessage,
                DomainController = NormalizeNullable(primaryDomainController),
                RequestSummaryJson = BuildValidationSummary(result),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent
            },
            cancellationToken);
    }

    public async Task<AdManagementConnectionParameters?> GetConnectionParametersAsync(
        CancellationToken cancellationToken = default)
    {
        var entity = await context.AdManagementSettings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return null;
        }

        string? password = null;
        if (!string.IsNullOrWhiteSpace(entity.EncryptedServiceAccountPassword))
        {
            try
            {
                password = secretProtector.Unprotect(entity.EncryptedServiceAccountPassword);
            }
            catch
            {
                password = null;
            }
        }

        return new AdManagementConnectionParameters(
            entity.DomainFqdn,
            entity.NetbiosDomainName,
            entity.DefaultNamingContext,
            entity.BaseDn,
            entity.UsersRootOu,
            entity.DisabledUsersOu,
            entity.GroupsSearchBase,
            entity.ComputersSearchBase,
            ParsePreferredDomainControllers(entity.PreferredDomainControllersJson),
            entity.UseSsl,
            entity.LdapPort,
            entity.ServiceAccountUserName,
            password);
    }

    public async Task RecordValidationResultAsync(
        AdManagementValidationResult result,
        AdManagementValidationRequest request,
        string? primaryDomainController,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.AdManagementSettings
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        await WriteValidationLogsAsync(
            result,
            request,
            primaryDomainController,
            entity,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    internal static AdManagementSettingsModel MapToModel(AdManagementSettings? entity)
    {
        if (entity is null)
        {
            return new AdManagementSettingsModel(
                IsConfigured: false,
                IsEnabled: false,
                DomainFqdn: null,
                DefaultUserCreationUpnSuffix: null,
                NetbiosDomainName: null,
                DefaultNamingContext: null,
                BaseDn: null,
                UsersRootOu: null,
                DisabledUsersOu: null,
                GroupsSearchBase: null,
                ComputersSearchBase: null,
                PreferredDomainControllers: Array.Empty<string>(),
                UseSsl: true,
                LdapPort: 636,
                ServiceAccountUserName: null,
                HasServiceAccountPassword: false,
                PowerShellHealthEnabled: false,
                PowerShellTimeoutSeconds: 30,
                LastValidatedAt: null,
                LastValidationStatus: null,
                LastValidationMessage: null,
                NotificationSettings: AdManagementNotificationSettingsSerializer.CreateDefault());
        }

        return new AdManagementSettingsModel(
            IsConfigured: true,
            IsEnabled: entity.IsEnabled,
            DomainFqdn: entity.DomainFqdn,
            DefaultUserCreationUpnSuffix: entity.DefaultUserCreationUpnSuffix,
            NetbiosDomainName: entity.NetbiosDomainName,
            DefaultNamingContext: entity.DefaultNamingContext,
            BaseDn: entity.BaseDn,
            UsersRootOu: entity.UsersRootOu,
            DisabledUsersOu: entity.DisabledUsersOu,
            GroupsSearchBase: entity.GroupsSearchBase,
            ComputersSearchBase: entity.ComputersSearchBase,
            PreferredDomainControllers: ParsePreferredDomainControllers(entity.PreferredDomainControllersJson),
            UseSsl: entity.UseSsl,
            LdapPort: entity.LdapPort,
            ServiceAccountUserName: entity.ServiceAccountUserName,
            HasServiceAccountPassword: !string.IsNullOrWhiteSpace(entity.EncryptedServiceAccountPassword),
            PowerShellHealthEnabled: entity.PowerShellHealthEnabled,
            PowerShellTimeoutSeconds: entity.PowerShellTimeoutSeconds,
            LastValidatedAt: entity.LastValidatedAt,
            LastValidationStatus: entity.LastValidationStatus,
            LastValidationMessage: entity.LastValidationMessage,
            NotificationSettings: AdManagementNotificationSettingsSerializer.Deserialize(
                entity.NotificationSettingsJson));
    }

    internal static List<AuditFieldChange> BuildNotificationSettingsAuditChanges(
        AdManagementNotificationSettings before,
        AdManagementNotificationSettings after)
    {
        var summary = AdManagementNotificationSettingsAuditBuilder.BuildRulesChangeSummary(before, after);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return [];
        }

        return
        [
            AuditChangeSummaryBuilder.PublicField(
                "NotificationRules",
                FormatRuleCount(before),
                FormatRuleCount(after)),
        ];
    }

    private static string FormatRuleCount(AdManagementNotificationSettings settings)
    {
        var enabled = settings.Rules.Count(rule => rule.IsEnabled);
        return $"{settings.Rules.Count} rules ({enabled} enabled)";
    }

    private static bool ValidateUpdateRequest(UpdateAdManagementSettingsRequest request, out string message)
    {
        if (request.LdapPort is < LdapPortMin or > LdapPortMax)
        {
            message = "LDAP port must be between 1 and 65535.";
            return false;
        }

        if (request.PowerShellTimeoutSeconds is < PowerShellTimeoutMin or > PowerShellTimeoutMax)
        {
            message = "PowerShell timeout must be between 5 and 300 seconds.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultUserCreationUpnSuffix))
        {
            var normalized = AdDefaultUpnSuffixNormalizer.Normalize(request.DefaultUserCreationUpnSuffix);
            if (string.IsNullOrWhiteSpace(normalized)
                || !AdDefaultUpnSuffixNormalizer.IsValidFormat(normalized))
            {
                message = "Default user creation UPN suffix must be a valid domain suffix.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static string? NormalizeDefaultUserCreationUpnSuffix(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : AdDefaultUpnSuffixNormalizer.Normalize(value);

    internal static List<string> NormalizePreferredDomainControllers(IReadOnlyList<string>? values)
    {
        var result = new List<string>();
        if (values is null)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    internal static IReadOnlyList<string> ParsePreferredDomainControllers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            if (parsed is null)
            {
                return Array.Empty<string>();
            }

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TruncateAuditDescription(string description) =>
        description.Length <= AuditDescriptionMaxLength
            ? description
            : description[..AuditDescriptionMaxLength];

    private const int ValidationMessageMaxLength = 1000;

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= ValidationMessageMaxLength
            ? trimmed
            : trimmed[..ValidationMessageMaxLength];
    }

    private static string BuildValidationSummary(AdManagementValidationResult result)
    {
        // Password values are intentionally excluded; only keys + statuses are recorded.
        var summary = new
        {
            isValid = result.IsValid,
            checkedAt = result.CheckedAt,
            message = SanitizeForLog(result.Message),
            details = result.Details
                .Select(d => new
                {
                    key = d.Key,
                    status = d.Status,
                    message = SanitizeForLog(d.Message)
                })
                .ToList()
        };

        return JsonSerializer.Serialize(summary);
    }

    private static string BuildSettingsRequestSummary(
        UpdateAdManagementSettingsRequest request,
        AdManagementSettings entity,
        IReadOnlyList<string> preferredControllers,
        bool passwordChanged)
    {
        // Password values are intentionally excluded from the summary.
        var summary = new
        {
            isEnabled = request.IsEnabled,
            domainFqdn = entity.DomainFqdn,
            netbiosDomainName = entity.NetbiosDomainName,
            defaultNamingContext = entity.DefaultNamingContext,
            baseDn = entity.BaseDn,
            usersRootOu = entity.UsersRootOu,
            disabledUsersOu = entity.DisabledUsersOu,
            groupsSearchBase = entity.GroupsSearchBase,
            computersSearchBase = entity.ComputersSearchBase,
            preferredDomainControllers = preferredControllers,
            useSsl = entity.UseSsl,
            ldapPort = entity.LdapPort,
            serviceAccountUserName = entity.ServiceAccountUserName,
            powerShellHealthEnabled = entity.PowerShellHealthEnabled,
            powerShellTimeoutSeconds = entity.PowerShellTimeoutSeconds,
            passwordChanged,
            passwordCleared = request.ClearServiceAccountPassword
        };

        return JsonSerializer.Serialize(summary);
    }
}
