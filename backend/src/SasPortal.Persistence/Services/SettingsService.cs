using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class SettingsService(
    AppDbContext context,
    ILdapService ldapService,
    ISecretProtector secretProtector) : ISettingsService
{
    private const string NationalIdApplicationSettingKey = "Directory:NationalIdAttribute";
    private const int AuditDescriptionMaxLength = 2000;
    private const int AuditIpAddressMaxLength = 64;
    private const int AuditUserAgentMaxLength = 1024;

    private static readonly HashSet<string> UpdatableApplicationSettingKeys = new(StringComparer.Ordinal)
    {
        NationalIdApplicationSettingKey
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

        return new SettingsOverview(MapLdapSetting(ldapSetting), appSettings);
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

        LdapValidationResult result;
        if (string.IsNullOrWhiteSpace(normalizedTestUserName) || string.IsNullOrWhiteSpace(normalizedTestPassword))
        {
            result = await ldapService.ValidateBindAsync(
                new LdapBindValidationRequest
                {
                    Host = request.Host.Trim(),
                    Port = request.Port,
                    UseSsl = request.UseSsl,
                    BindUserName = request.BindUserName.Trim(),
                    BindUserDomain = NormalizeNullable(request.BindUserDomain),
                    BindPassword = bindPassword!
                },
                cancellationToken);
        }
        else
        {
            result = await ldapService.ValidateAsync(
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
        }

        return new ValidateLdapSettingsResult(result.IsValid, result.Message);
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

            var setting = await context.ApplicationSettings
                .FirstOrDefaultAsync(x => x.Key == key && !x.IsDeleted, cancellationToken);

            if (setting is null)
            {
                setting = new ApplicationSetting
                {
                    Key = key,
                    Value = NormalizeNullable(item.Value),
                    ValueType = item.ValueType,
                    Description = "LDAP attribute name that stores the national identity value.",
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
                setting.Value = NormalizeNullable(item.Value);
                setting.ValueType = item.ValueType;
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
                    EntityId = key,
                    Description = TruncateAuditDescription($"Application setting updated: {key}. Value changed."),
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
