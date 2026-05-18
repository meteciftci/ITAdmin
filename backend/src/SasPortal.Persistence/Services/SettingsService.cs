using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class SettingsService(
    AppDbContext context,
    ILdapService ldapService,
    ISecretProtector secretProtector,
    ILogger<SettingsService> logger) : ISettingsService
{
    private const string NationalIdApplicationSettingKey = "Directory:NationalIdAttribute";
    private const string BrandingApplicationNameKey = "Branding:ApplicationName";
    private const string BrandingBrowserTitleKey = "Branding:BrowserTitle";
    private const string BrandingLogoUrlKey = "Branding:LogoUrl";
    private const string BrandingFaviconUrlKey = "Branding:FaviconUrl";
    private const string BrandingForgotPasswordUrlKey = "Branding:ForgotPasswordUrl";
    private const string BrandingFooterTextKey = "Branding:FooterText";
    private const string DefaultBrandingApplicationName = "SAS Portal v2";
    private const string DefaultBrandingBrowserTitle = "SAS Portal v2";
    private const string DefaultBrandingFaviconUrl = "/favicon.svg";
    private const int BrandingTextMaxLength = 100;
    private const int BrandingFooterTextMaxLength = 200;
    private const int BrandingUrlMaxLength = 500;
    private const int AuditDescriptionMaxLength = 2000;
    private const int AuditSettingValueMaxLength = 256;
    private const int AuditIpAddressMaxLength = 64;
    private const int AuditUserAgentMaxLength = 1024;
    private const string AuditSettingMissingValue = "<none>";

    private static readonly HashSet<string> UpdatableApplicationSettingKeys = new(StringComparer.Ordinal)
    {
        NationalIdApplicationSettingKey,
        BrandingApplicationNameKey,
        BrandingBrowserTitleKey,
        BrandingLogoUrlKey,
        BrandingFaviconUrlKey,
        BrandingForgotPasswordUrlKey,
        BrandingFooterTextKey
    };

    // Defense-in-depth: future settings whose key contains any of these tokens are treated
    // as secrets and their values are never written to audit logs.
    private static readonly string[] SensitiveApplicationSettingKeyTokens =
    {
        "password",
        "secret",
        "token",
        "credential",
        "apikey",
        "privatekey"
    };

    public async Task<SettingsOverview> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var ldapSetting = await context.LdapSettings
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var appSettings = await context.ApplicationSettings
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Key)
            .Select(x => new ApplicationSettingItem(
                x.Key,
                x.IsEncrypted ? null : x.Value,
                x.ValueType,
                x.Description,
                x.IsEncrypted,
                x.IsSystem,
                x.IsActive))
            .ToListAsync(cancellationToken);

        var branding = BuildBrandingSettings(appSettings);
        var sessionSecurity = SessionSecuritySettingsHelper.ReadFromItems(appSettings, logger);
        return new SettingsOverview(MapLdapSetting(ldapSetting), appSettings, branding, sessionSecurity);
    }

    public async Task<BrandingSettings> GetBrandingSettingsAsync(CancellationToken cancellationToken = default)
    {
        var appSettings = await context.ApplicationSettings
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .Where(x =>
                x.Key == BrandingApplicationNameKey
                || x.Key == BrandingBrowserTitleKey
                || x.Key == BrandingLogoUrlKey
                || x.Key == BrandingFaviconUrlKey
                || x.Key == BrandingForgotPasswordUrlKey
                || x.Key == BrandingFooterTextKey)
            .Select(x => new ApplicationSettingItem(
                x.Key,
                x.IsEncrypted ? null : x.Value,
                x.ValueType,
                x.Description,
                x.IsEncrypted,
                x.IsSystem,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return BuildBrandingSettings(appSettings);
    }

    public async Task<UpdateSettingsResult> UpdateLdapSettingsAsync(
        UpdateLdapSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsLdapUpdateRequestValid(request, out var validationMessage))
        {
            return new UpdateSettingsResult(false, validationMessage, null);
        }

        var ldapSetting = await context.LdapSettings
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var isNew = ldapSetting is null;

        if (isNew && string.IsNullOrWhiteSpace(request.BindPassword))
        {
            return new UpdateSettingsResult(false, "Bind password is required for a new LDAP setting.", null);
        }

        var name = request.Name.Trim();
        var host = request.Host.Trim();
        var baseDn = request.BaseDn.Trim();
        var userSearchFilter = request.UserSearchFilter.Trim();
        var bindUserName = request.BindUserName.Trim();
        var userSearchBase = request.UserSearchBase.Trim();
        var bindUserDomain = NormalizeNullable(request.BindUserDomain);
        var description = NormalizeNullable(request.Description);
        var providedBindPassword = NormalizeNullable(request.BindPassword);

        if (ldapSetting is null)
        {
            ldapSetting = new LdapSetting
            {
                Name = name,
                Host = host,
                Port = request.Port,
                UseSsl = request.UseSsl,
                BaseDn = baseDn,
                UserSearchBase = userSearchBase ?? string.Empty,
                UserSearchFilter = userSearchFilter,
                BindUserName = bindUserName,
                BindUserDomain = bindUserDomain,
                EncryptedBindPassword = secretProtector.Protect(providedBindPassword!),
                Description = description,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = request.ActorUserName ?? "system"
            };

            await context.LdapSettings.AddAsync(ldapSetting, cancellationToken);
        }
        else
        {
            var oldName = ldapSetting.Name;
            var oldHost = ldapSetting.Host;
            var oldPort = ldapSetting.Port;
            var oldUseSsl = ldapSetting.UseSsl;
            var oldBindUserName = ldapSetting.BindUserName;
            var oldBindUserDomain = ldapSetting.BindUserDomain;

            ldapSetting.Name = name;
            ldapSetting.Host = host;
            ldapSetting.Port = request.Port;
            ldapSetting.UseSsl = request.UseSsl;
            ldapSetting.BaseDn = baseDn;
            ldapSetting.UserSearchBase = userSearchBase ?? string.Empty;
            ldapSetting.UserSearchFilter = userSearchFilter;
            ldapSetting.BindUserName = bindUserName;
            ldapSetting.BindUserDomain = bindUserDomain;
            ldapSetting.Description = description;
            ldapSetting.IsActive = true;
            ldapSetting.UpdatedAt = now;
            ldapSetting.UpdatedBy = request.ActorUserName ?? "system";

            if (!string.IsNullOrWhiteSpace(providedBindPassword))
            {
                ldapSetting.EncryptedBindPassword = secretProtector.Protect(providedBindPassword);
            }

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Update",
                    EntityName = "LdapSetting",
                    EntityId = ldapSetting.Id.ToString(),
                    Description = BuildLdapUpdateAuditDescription(
                        ldapSetting.Name,
                        oldHost,
                        ldapSetting.Host,
                        oldPort,
                        ldapSetting.Port,
                        oldUseSsl,
                        ldapSetting.UseSsl,
                        !string.Equals(oldBindUserName, ldapSetting.BindUserName, StringComparison.Ordinal)
                        || !string.Equals(oldBindUserDomain, ldapSetting.BindUserDomain, StringComparison.Ordinal),
                        !string.IsNullOrWhiteSpace(providedBindPassword)),
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                    UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);
        }

        if (isNew)
        {
            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Update",
                    EntityName = "LdapSetting",
                    EntityId = ldapSetting.Id.ToString(),
                    Description = BuildLdapUpdateAuditDescription(
                        ldapSetting.Name,
                        null,
                        ldapSetting.Host,
                        null,
                        ldapSetting.Port,
                        null,
                        ldapSetting.UseSsl,
                        true,
                        true),
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                    UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        var settings = await GetSettingsAsync(cancellationToken);
        return new UpdateSettingsResult(true, "LDAP settings updated.", settings);
    }

    public async Task<ValidateLdapSettingsResult> ValidateLdapSettingsAsync(
        ValidateLdapSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsLdapValidateRequestValid(request, out var validationMessage))
        {
            return new ValidateLdapSettingsResult(false, validationMessage);
        }

        var bindPassword = NormalizeNullable(request.BindPassword);
        if (string.IsNullOrWhiteSpace(bindPassword))
        {
            var existing = await context.LdapSettings
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                return new ValidateLdapSettingsResult(false, "Bind password is required when no active LDAP setting exists.");
            }

            try
            {
                bindPassword = secretProtector.Unprotect(existing.EncryptedBindPassword);
            }
            catch
            {
                return new ValidateLdapSettingsResult(false, "LDAP validation could not be completed.");
            }
        }

        var normalizedTestUserName = NormalizeNullable(request.TestUserName);
        var normalizedTestPassword = NormalizeNullable(request.TestPassword);

        var searchBasesRequest = new LdapSearchBasesValidationRequest
        {
            Host = request.Host.Trim(),
            Port = request.Port,
            UseSsl = request.UseSsl,
            BaseDn = request.BaseDn.Trim(),
            UserSearchBase = NormalizeNullable(request.UserSearchBase) ?? string.Empty,
            BindUserName = request.BindUserName.Trim(),
            BindUserDomain = NormalizeNullable(request.BindUserDomain),
            BindPassword = bindPassword!
        };

        var basesResult = await ldapService.ValidateSearchBasesAsync(searchBasesRequest, cancellationToken);
        if (!basesResult.IsValid)
        {
            return new ValidateLdapSettingsResult(false, basesResult.Message);
        }

        if (string.IsNullOrWhiteSpace(normalizedTestUserName) || string.IsNullOrWhiteSpace(normalizedTestPassword))
        {
            return new ValidateLdapSettingsResult(true, basesResult.Message);
        }

        var userResult = await ldapService.ValidateAsync(
            new LdapValidationRequest
            {
                Host = request.Host.Trim(),
                Port = request.Port,
                UseSsl = request.UseSsl,
                BaseDn = request.BaseDn.Trim(),
                UserSearchBase = NormalizeNullable(request.UserSearchBase) ?? string.Empty,
                UserSearchFilter = request.UserSearchFilter.Trim(),
                BindUserName = request.BindUserName.Trim(),
                BindUserDomain = NormalizeNullable(request.BindUserDomain),
                BindPassword = bindPassword!,
                TestUserName = normalizedTestUserName!,
                TestPassword = normalizedTestPassword!
            },
            cancellationToken);

        return new ValidateLdapSettingsResult(userResult.IsValid, userResult.Message);
    }

    public async Task<UpdateSettingsResult> UpdateApplicationSettingsAsync(
        UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return new UpdateSettingsResult(false, "At least one application setting item is required.", null);
        }

        var now = DateTime.UtcNow;

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                return new UpdateSettingsResult(false, "Application setting key is required.", null);
            }

            var key = item.Key.Trim();
            if (!UpdatableApplicationSettingKeys.Contains(key))
            {
                return new UpdateSettingsResult(false, $"Application setting key is not allowed: {key}.", null);
            }

            if (key == NationalIdApplicationSettingKey && item.ValueType != SettingValueType.String)
            {
                return new UpdateSettingsResult(false, "Directory:NationalIdAttribute must use String value type.", null);
            }

            if (key == BrandingApplicationNameKey
                || key == BrandingBrowserTitleKey
                || key == BrandingLogoUrlKey
                || key == BrandingFaviconUrlKey
                || key == BrandingForgotPasswordUrlKey
                || key == BrandingFooterTextKey)
            {
                if (item.ValueType != SettingValueType.String)
                {
                    return new UpdateSettingsResult(false, $"Branding setting {key} must use String value type.", null);
                }

                if (!ValidateBrandingValue(key, item.Value, out var brandingValidationMessage))
                {
                    return new UpdateSettingsResult(false, brandingValidationMessage, null);
                }
            }

            var setting = await context.ApplicationSettings
                .FirstOrDefaultAsync(x => x.Key == key && !x.IsDeleted, cancellationToken);

            var oldValue = NormalizeNullable(setting?.Value);
            var newValue = NormalizeNullable(item.Value);
            // Sensitivity is captured from the persisted state so that values previously stored as
            // encrypted can never leak into audit, even if the incoming write flips IsEncrypted.
            var wasEncrypted = setting?.IsEncrypted ?? false;

            var description = ResolveApplicationSettingDescription(key);

            if (setting is null)
            {
                setting = new ApplicationSetting
                {
                    Key = key,
                    Value = newValue,
                    ValueType = item.ValueType,
                    Description = description,
                    IsEncrypted = false,
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = request.ActorUserName ?? "system"
                };

                await context.ApplicationSettings.AddAsync(setting, cancellationToken);
            }
            else
            {
                setting.Value = newValue;
                setting.ValueType = item.ValueType;
                // Self-heal description in case a previous write stored an incorrect value.
                setting.Description = description;
                setting.IsEncrypted = false;
                setting.IsSystem = true;
                setting.IsActive = true;
                setting.UpdatedAt = now;
                setting.UpdatedBy = request.ActorUserName ?? "system";
            }

            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                // No effective change; skip audit so the log does not grow with no-op writes.
                continue;
            }

            var isSensitive = IsSensitiveApplicationSetting(key, wasEncrypted);

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Update",
                    EntityName = "ApplicationSetting",
                    EntityId = key,
                    Description = BuildApplicationSettingAuditDescription(key, oldValue, newValue, isSensitive),
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                    UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        var settings = await GetSettingsAsync(cancellationToken);
        return new UpdateSettingsResult(true, "Application settings updated.", settings);
    }

    public async Task<UpdateSettingsResult> UpdateSessionSecuritySettingsAsync(
        UpdateSessionSecuritySettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = SessionSecuritySettingsHelper.ValidateUpdate(
            request.AccessTokenMinutes,
            request.IdleTimeoutMinutes,
            request.IdleWarningSeconds,
            request.SessionRefreshTokenHours,
            request.RememberMeRefreshTokenDays);

        if (validationError is not null)
        {
            return new UpdateSettingsResult(false, validationError, null);
        }

        var appSettingItems = await context.ApplicationSettings
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Key)
            .Select(x => new ApplicationSettingItem(
                x.Key,
                x.IsEncrypted ? null : x.Value,
                x.ValueType,
                x.Description,
                x.IsEncrypted,
                x.IsSystem,
                x.IsActive))
            .ToListAsync(cancellationToken);

        var existingActiveSecurityKeys = appSettingItems
            .Where(x => SecuritySettingKeys.AllSet.Contains(x.Key) && x.IsActive)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);

        var hasMissingSecuritySettings = SecuritySettingKeys.All.Any(k => !existingActiveSecurityKeys.Contains(k));

        var before = SessionSecuritySettingsHelper.ReadFromItems(appSettingItems, logger);
        var after = new SessionSecuritySettings(
            request.AccessTokenMinutes,
            request.IdleTimeoutMinutes,
            request.IdleWarningSeconds,
            request.SessionRefreshTokenHours,
            request.RememberMeRefreshTokenDays,
            request.RememberMeEnabled);

        var auditDescription = SessionSecuritySettingsHelper.BuildAuditDescription(before, after);
        if (string.IsNullOrWhiteSpace(auditDescription) && !hasMissingSecuritySettings)
        {
            var unchanged = await GetSettingsAsync(cancellationToken);
            return new UpdateSettingsResult(true, "Session security settings are unchanged.", unchanged);
        }

        var now = DateTime.UtcNow;

        await UpsertSessionSecuritySettingAsync(
            SecuritySettingKeys.AccessTokenMinutes,
            after.AccessTokenMinutes.ToString(CultureInfo.InvariantCulture),
            SettingValueType.Number,
            "Access token lifetime in minutes.",
            request,
            now,
            cancellationToken);

        await UpsertSessionSecuritySettingAsync(
            SecuritySettingKeys.IdleTimeoutMinutes,
            after.IdleTimeoutMinutes.ToString(CultureInfo.InvariantCulture),
            SettingValueType.Number,
            "Idle timeout in minutes.",
            request,
            now,
            cancellationToken);

        await UpsertSessionSecuritySettingAsync(
            SecuritySettingKeys.IdleWarningSeconds,
            after.IdleWarningSeconds.ToString(CultureInfo.InvariantCulture),
            SettingValueType.Number,
            "Seconds before idle timeout to show a warning.",
            request,
            now,
            cancellationToken);

        await UpsertSessionSecuritySettingAsync(
            SecuritySettingKeys.SessionRefreshTokenHours,
            after.SessionRefreshTokenHours.ToString(CultureInfo.InvariantCulture),
            SettingValueType.Number,
            "Browser session refresh token lifetime in hours.",
            request,
            now,
            cancellationToken);

        await UpsertSessionSecuritySettingAsync(
            SecuritySettingKeys.RememberMeRefreshTokenDays,
            after.RememberMeRefreshTokenDays.ToString(CultureInfo.InvariantCulture),
            SettingValueType.Number,
            "Remember me refresh token lifetime in days.",
            request,
            now,
            cancellationToken);

        await UpsertSessionSecuritySettingAsync(
            SecuritySettingKeys.RememberMeEnabled,
            after.RememberMeEnabled.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            SettingValueType.Boolean,
            "Whether the remember me option is enabled on the login screen.",
            request,
            now,
            cancellationToken);

        var finalAuditDescription = string.IsNullOrWhiteSpace(auditDescription)
            ? "Session security settings initialized with current values."
            : auditDescription;

        var successMessage = string.IsNullOrWhiteSpace(auditDescription)
            ? "Session security settings initialized."
            : "Session security settings updated.";

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "SessionSecuritySettings",
                EntityId = "SessionSecurity",
                Description = TruncateAuditDescription(finalAuditDescription),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        var settings = await GetSettingsAsync(cancellationToken);
        return new UpdateSettingsResult(true, successMessage, settings);
    }

    public async Task<SessionSecuritySettings> GetSessionSecuritySettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var keys = SecuritySettingKeys.All;
        var appSettings = await context.ApplicationSettings
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && keys.Contains(x.Key))
            .OrderBy(x => x.Key)
            .Select(x => new ApplicationSettingItem(
                x.Key,
                x.IsEncrypted ? null : x.Value,
                x.ValueType,
                x.Description,
                x.IsEncrypted,
                x.IsSystem,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return SessionSecuritySettingsHelper.ReadFromItems(appSettings, logger);
    }

    public async Task<AuthSessionOptions> GetAuthSessionOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionSecurity = await GetSessionSecuritySettingsAsync(cancellationToken);
            return new AuthSessionOptions(
                sessionSecurity.RememberMeEnabled,
                sessionSecurity.IdleTimeoutMinutes,
                sessionSecurity.IdleWarningSeconds,
                sessionSecurity.AccessTokenMinutes);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to read auth session options from settings. Returning defaults.");
            return new AuthSessionOptions(
                SessionSecurityDefaults.RememberMeEnabled,
                SessionSecurityDefaults.IdleTimeoutMinutes,
                SessionSecurityDefaults.IdleWarningSeconds,
                SessionSecurityDefaults.AccessTokenMinutes);
        }
    }

    private async Task UpsertSessionSecuritySettingAsync(
        string key,
        string? value,
        SettingValueType valueType,
        string description,
        UpdateSessionSecuritySettingsRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var setting = await context.ApplicationSettings
            .FirstOrDefaultAsync(x => x.Key == key && !x.IsDeleted, cancellationToken);

        if (setting is null)
        {
            setting = new ApplicationSetting
            {
                Key = key,
                Value = value,
                ValueType = valueType,
                Description = description,
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = request.ActorUserName ?? "system"
            };

            await context.ApplicationSettings.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = value;
            setting.ValueType = valueType;
            setting.Description = description;
            setting.IsEncrypted = false;
            setting.IsSystem = true;
            setting.IsActive = true;
            setting.UpdatedAt = now;
            setting.UpdatedBy = request.ActorUserName ?? "system";
        }
    }

    public async Task<BrandingLogoUploadResult> UploadBrandingLogoAsync(
        UploadBrandingLogoRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var extension = request.FileExtension.ToLowerInvariant();
        var safeFileName = $"logo-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";
        var brandingUploadsFolder = request.UploadDirectoryPath;
        Directory.CreateDirectory(brandingUploadsFolder);

        var filePath = Path.Combine(brandingUploadsFolder, safeFileName);
        await File.WriteAllBytesAsync(filePath, request.Content, cancellationToken);

        var logoUrl = $"/uploads/branding/{safeFileName}";
        var setting = await context.ApplicationSettings
            .FirstOrDefaultAsync(x => x.Key == BrandingLogoUrlKey && !x.IsDeleted, cancellationToken);

        var previousLogoUrl = setting?.Value;
        if (setting is null)
        {
            setting = new ApplicationSetting
            {
                Key = BrandingLogoUrlKey,
                Value = logoUrl,
                ValueType = SettingValueType.String,
                Description = "Application branding logo URL.",
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = request.ActorUserName ?? "system"
            };
            await context.ApplicationSettings.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = logoUrl;
            setting.ValueType = SettingValueType.String;
            setting.IsEncrypted = false;
            setting.IsSystem = true;
            setting.IsActive = true;
            setting.UpdatedAt = now;
            setting.UpdatedBy = request.ActorUserName ?? "system";
        }

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "ApplicationSetting",
                EntityId = BrandingLogoUrlKey,
                Description = TruncateAuditDescription($"Application branding logo updated. File: {safeFileName}"),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        TryDeleteOldBrandingFile(previousLogoUrl, logoUrl, brandingUploadsFolder);

        return new BrandingLogoUploadResult(logoUrl);
    }

    public async Task<BrandingFaviconUploadResult> UploadBrandingFaviconAsync(
        UploadBrandingFaviconRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var extension = request.FileExtension.ToLowerInvariant();
        var safeFileName = $"favicon-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";
        var brandingUploadsFolder = request.UploadDirectoryPath;
        Directory.CreateDirectory(brandingUploadsFolder);

        var filePath = Path.Combine(brandingUploadsFolder, safeFileName);
        await File.WriteAllBytesAsync(filePath, request.Content, cancellationToken);

        var faviconUrl = $"/uploads/branding/{safeFileName}";
        var setting = await context.ApplicationSettings
            .FirstOrDefaultAsync(x => x.Key == BrandingFaviconUrlKey && !x.IsDeleted, cancellationToken);

        var previousFaviconUrl = setting?.Value;
        if (setting is null)
        {
            setting = new ApplicationSetting
            {
                Key = BrandingFaviconUrlKey,
                Value = faviconUrl,
                ValueType = SettingValueType.String,
                Description = "Application branding favicon URL.",
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = request.ActorUserName ?? "system"
            };
            await context.ApplicationSettings.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = faviconUrl;
            setting.ValueType = SettingValueType.String;
            setting.IsEncrypted = false;
            setting.IsSystem = true;
            setting.IsActive = true;
            setting.UpdatedAt = now;
            setting.UpdatedBy = request.ActorUserName ?? "system";
        }

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "ApplicationSetting",
                EntityId = BrandingFaviconUrlKey,
                Description = TruncateAuditDescription($"Application branding favicon updated. File: {safeFileName}"),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        TryDeleteOldBrandingFile(previousFaviconUrl, faviconUrl, brandingUploadsFolder);

        return new BrandingFaviconUploadResult(faviconUrl);
    }

    private static bool IsLdapUpdateRequestValid(UpdateLdapSettingsRequest request, out string message)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Host) ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.UserSearchFilter) ||
            string.IsNullOrWhiteSpace(request.BindUserName))
        {
            message = "Required LDAP fields are missing.";
            return false;
        }

        if (request.Port is < 1 or > 65535)
        {
            message = "LDAP port must be between 1 and 65535.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool IsLdapValidateRequestValid(ValidateLdapSettingsRequest request, out string message)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Host) ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.UserSearchFilter) ||
            string.IsNullOrWhiteSpace(request.BindUserName))
        {
            message = "Required LDAP validation fields are missing.";
            return false;
        }

        if (request.Port is < 1 or > 65535)
        {
            message = "LDAP port must be between 1 and 65535.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static LdapSettingsModel? MapLdapSetting(LdapSetting? setting)
    {
        if (setting is null)
        {
            return null;
        }

        return new LdapSettingsModel(
            setting.Id,
            setting.Name,
            setting.Host,
            setting.Port,
            setting.UseSsl,
            setting.BaseDn,
            setting.UserSearchBase,
            setting.UserSearchFilter,
            setting.BindUserName,
            setting.BindUserDomain,
            !string.IsNullOrWhiteSpace(setting.EncryptedBindPassword),
            setting.Description,
            setting.IsActive);
    }

    private static string BuildLdapUpdateAuditDescription(
        string name,
        string? oldHost,
        string newHost,
        int? oldPort,
        int newPort,
        bool? oldUseSsl,
        bool newUseSsl,
        bool bindUserChanged,
        bool bindPasswordChanged)
    {
        var hostPart = oldHost is null ? $"Host: <none> -> {newHost}." : $"Host: {oldHost} -> {newHost}.";
        var portPart = oldPort is null ? $"Port: <none> -> {newPort}." : $"Port: {oldPort} -> {newPort}.";
        var sslPart = oldUseSsl is null
            ? $"SSL: <none> -> {FormatSsl(newUseSsl)}."
            : $"SSL: {FormatSsl(oldUseSsl.Value)} -> {FormatSsl(newUseSsl)}.";

        var description = $"LDAP settings updated: {name}. {hostPart} {portPart} {sslPart}";
        if (bindUserChanged)
        {
            description += " Bind user changed.";
        }

        if (bindPasswordChanged)
        {
            description += " Bind password changed.";
        }

        return TruncateAuditDescription(description.Trim());
    }

    private static string FormatSsl(bool value) => value ? "Enabled" : "Disabled";

    private static BrandingSettings BuildBrandingSettings(IEnumerable<ApplicationSettingItem> settings)
    {
        var map = settings
            .Where(x => x.IsActive)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        var applicationName = ResolveBrandingText(map, BrandingApplicationNameKey, DefaultBrandingApplicationName);
        var browserTitle = ResolveBrandingText(map, BrandingBrowserTitleKey, DefaultBrandingBrowserTitle);
        var logoUrl = ResolveBrandingAssetUrl(map, BrandingLogoUrlKey, fallback: null, allowRelative: true);
        var faviconUrl = ResolveBrandingAssetUrl(map, BrandingFaviconUrlKey, fallback: DefaultBrandingFaviconUrl, allowRelative: true);
        var forgotPasswordUrl = ResolveBrandingAssetUrl(map, BrandingForgotPasswordUrlKey, fallback: null, allowRelative: false);
        var footerText = ResolveBrandingFooterText(map);
        return new BrandingSettings(applicationName, browserTitle, logoUrl, faviconUrl, forgotPasswordUrl, footerText);
    }

    private static string ResolveDefaultBrandingFooterText() =>
        string.Create(CultureInfo.InvariantCulture, $"© {DateTime.UtcNow.Year} SAS Portal");

    private static string ResolveBrandingFooterText(IReadOnlyDictionary<string, string?> map)
    {
        if (!map.TryGetValue(BrandingFooterTextKey, out var value))
        {
            return ResolveDefaultBrandingFooterText();
        }

        var normalized = NormalizeNullable(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ResolveDefaultBrandingFooterText();
        }

        return normalized.Length > BrandingFooterTextMaxLength
            ? normalized[..BrandingFooterTextMaxLength]
            : normalized;
    }

    private static string ResolveBrandingText(
        IReadOnlyDictionary<string, string?> map,
        string key,
        string fallback)
    {
        if (!map.TryGetValue(key, out var value))
        {
            return fallback;
        }

        var normalized = NormalizeNullable(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        return normalized.Length > BrandingTextMaxLength ? normalized[..BrandingTextMaxLength] : normalized;
    }

    private static string? ResolveBrandingAssetUrl(
        IReadOnlyDictionary<string, string?> map,
        string key,
        string? fallback,
        bool allowRelative)
    {
        if (!map.TryGetValue(key, out var value))
        {
            return fallback;
        }

        var normalized = NormalizeNullable(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        return IsAllowedBrandingUrl(normalized, allowRelative) ? normalized : fallback;
    }

    private static bool ValidateBrandingValue(string key, string? value, out string message)
    {
        var normalized = NormalizeNullable(value);
        if (key == BrandingApplicationNameKey || key == BrandingBrowserTitleKey)
        {
            if (normalized is not null && normalized.Length > BrandingTextMaxLength)
            {
                message = $"{key} must be at most {BrandingTextMaxLength} characters.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        if (key == BrandingFooterTextKey)
        {
            if (normalized is not null && normalized.Length > BrandingFooterTextMaxLength)
            {
                message = $"{key} must be at most {BrandingFooterTextMaxLength} characters.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        if (normalized is null)
        {
            message = string.Empty;
            return true;
        }

        if (normalized.Length > BrandingUrlMaxLength)
        {
            message = $"{key} must be at most {BrandingUrlMaxLength} characters.";
            return false;
        }

        var allowRelative = key != BrandingForgotPasswordUrlKey;
        if (!IsAllowedBrandingUrl(normalized, allowRelative))
        {
            message = allowRelative
                ? $"{key} must be an http/https URL or an absolute relative path starting with '/'."
                : $"{key} must be an http/https URL.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool IsAllowedBrandingUrl(string value, bool allowRelative)
    {
        if (allowRelative && value.StartsWith('/'))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static void TryDeleteOldBrandingFile(string? previousUrl, string newUrl, string brandingUploadsFolder)
    {
        var normalizedPrevious = NormalizeNullable(previousUrl);
        if (string.IsNullOrWhiteSpace(normalizedPrevious)
            || string.Equals(normalizedPrevious, newUrl, StringComparison.Ordinal))
        {
            return;
        }

        if (!normalizedPrevious.StartsWith("/uploads/branding/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var oldFileName = normalizedPrevious["/uploads/branding/".Length..];
        if (string.IsNullOrWhiteSpace(oldFileName))
        {
            return;
        }

        var oldPath = Path.Combine(brandingUploadsFolder, oldFileName);
        if (File.Exists(oldPath))
        {
            try
            {
                File.Delete(oldPath);
            }
            catch
            {
            }
        }
    }


    private static string ResolveApplicationSettingDescription(string key) => key switch
    {
        NationalIdApplicationSettingKey => "LDAP attribute name that stores the national identity value.",
        BrandingApplicationNameKey => "Application display name.",
        BrandingBrowserTitleKey => "Browser title shown in the tab.",
        BrandingLogoUrlKey => "Application branding logo URL.",
        BrandingFaviconUrlKey => "Application branding favicon URL.",
        BrandingForgotPasswordUrlKey => "External forgot password URL shown on the login page.",
        BrandingFooterTextKey => "Footer text shown centered at the bottom of the application layout.",
        _ => string.Empty
    };

    private static bool IsSensitiveApplicationSetting(string key, bool isEncrypted)
    {
        if (isEncrypted)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        // Sensitivity is based on the last key segment's suffix so non-secret keys that merely
        // mention a secret-related word (e.g. "Branding:ForgotPasswordUrl") are not misclassified.
        var separatorIndex = key.LastIndexOf(':');
        var lastSegment = separatorIndex >= 0 && separatorIndex + 1 < key.Length
            ? key[(separatorIndex + 1)..]
            : key;

        foreach (var token in SensitiveApplicationSettingKeyTokens)
        {
            if (lastSegment.EndsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatAuditSettingValue(string? value)
    {
        var normalized = NormalizeNullable(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return AuditSettingMissingValue;
        }

        return normalized.Length <= AuditSettingValueMaxLength
            ? normalized
            : $"{normalized[..(AuditSettingValueMaxLength - 3)]}...";
    }

    private static string BuildApplicationSettingAuditDescription(
        string key,
        string? oldValue,
        string? newValue,
        bool isSensitive)
    {
        if (isSensitive)
        {
            return TruncateAuditDescription($"Application setting updated: {key}. Value changed.");
        }

        var oldFormatted = FormatAuditSettingValue(oldValue);
        var newFormatted = FormatAuditSettingValue(newValue);
        return TruncateAuditDescription(
            $"Application setting updated: {key}. Value: {oldFormatted} -> {newFormatted}.");
    }

    private static string TruncateAuditDescription(string description) =>
        description.Length <= AuditDescriptionMaxLength
            ? description
            : $"{description[..(AuditDescriptionMaxLength - 3)]}...";

    private static string? TruncateAuditIpAddress(string? ipAddress) =>
        TruncateNullable(ipAddress, AuditIpAddressMaxLength);

    private static string? TruncateAuditUserAgent(string? userAgent) =>
        TruncateNullable(userAgent, AuditUserAgentMaxLength);

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
}
