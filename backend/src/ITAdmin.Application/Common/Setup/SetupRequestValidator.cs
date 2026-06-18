using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using Microsoft.Extensions.Configuration;

namespace ITAdmin.Application.Common.Setup;

public static class SetupRequestValidator
{
    public static bool TryValidateCompleteSetupRequest(
        CompleteSetupRequest request,
        out string message,
        out string? messageKey)
    {
        message = string.Empty;
        messageKey = null;

        if (string.IsNullOrWhiteSpace(request.SetupKey))
        {
            message = "Invalid setup request.";
            messageKey = SetupApiMessageKeys.Validation.InvalidSetupRequest;
            return false;
        }

        if (!TryValidateLdapSettings(request.Ldap, out message, out messageKey))
        {
            message = string.IsNullOrWhiteSpace(message) ? "Invalid setup request." : message;
            messageKey ??= SetupApiMessageKeys.Validation.InvalidLdapSettings;
            return false;
        }

        if (request.AdminUsers is null || request.AdminUsers.Count == 0)
        {
            message = "At least one admin user is required.";
            messageKey = SetupApiMessageKeys.Validation.AdminUsersRequired;
            return false;
        }

        if (!TryValidateUniqueAdminUsers(request.AdminUsers, out message, out messageKey))
        {
            return false;
        }

        if (!TryValidateModules(request.Modules, out message, out messageKey))
        {
            return false;
        }

        return true;
    }

    public static bool TryValidateLdapSettings(
        CompleteSetupLdapSettings? ldap,
        out string message,
        out string? messageKey)
    {
        message = string.Empty;
        messageKey = null;

        if (ldap is null ||
            string.IsNullOrWhiteSpace(ldap.Host) ||
            string.IsNullOrWhiteSpace(ldap.BaseDn) ||
            string.IsNullOrWhiteSpace(ldap.UserSearchBase) ||
            string.IsNullOrWhiteSpace(ldap.UserSearchFilter) ||
            string.IsNullOrWhiteSpace(ldap.BindUserName) ||
            string.IsNullOrWhiteSpace(ldap.BindPassword))
        {
            message = "Invalid LDAP settings.";
            messageKey = SetupApiMessageKeys.Validation.InvalidLdapSettings;
            return false;
        }

        return true;
    }

    public static bool TryValidateUniqueAdminUsers(
        IReadOnlyList<CompleteSetupAdminUser> adminUsers,
        out string message,
        out string? messageKey)
    {
        message = string.Empty;
        messageKey = null;

        var seenDirectoryObjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var adminUser in adminUsers)
        {
            if (string.IsNullOrWhiteSpace(adminUser.UserName))
            {
                message = "Invalid setup request.";
                messageKey = SetupApiMessageKeys.Validation.InvalidSetupRequest;
                return false;
            }

            var normalizedDirectoryObjectId = NormalizeOptional(adminUser.DirectoryObjectId);
            if (!string.IsNullOrWhiteSpace(normalizedDirectoryObjectId))
            {
                if (!seenDirectoryObjectIds.Add(normalizedDirectoryObjectId))
                {
                    message = "Duplicate admin user selection is not allowed.";
                    messageKey = SetupApiMessageKeys.Validation.DuplicateAdminUser;
                    return false;
                }

                continue;
            }

            var normalizedUserName = NormalizeUserName(adminUser.UserName);
            if (!seenUserNames.Add(normalizedUserName))
            {
                message = "Duplicate admin user selection is not allowed.";
                messageKey = SetupApiMessageKeys.Validation.DuplicateAdminUser;
                return false;
            }
        }

        return true;
    }

    public static bool TryValidateModules(
        CompleteSetupModulesSettings? modules,
        out string message,
        out string? messageKey)
    {
        message = string.Empty;
        messageKey = null;

        var adManagement = modules?.AdManagement;
        if (adManagement is null || !adManagement.IsEnabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(adManagement.UsersSearchBase) ||
            string.IsNullOrWhiteSpace(adManagement.GroupsSearchBase) ||
            string.IsNullOrWhiteSpace(adManagement.ComputersSearchBase))
        {
            message = "AD Management module is missing required fields.";
            messageKey = SetupApiMessageKeys.Validation.AdManagementModuleMissingRequiredFields;
            return false;
        }

        return true;
    }

    public static SetupKeyValidationOutcome ValidateSetupKey(
        ISetupKeyValidator setupKeyValidator,
        IConfiguration configuration,
        string setupKey)
    {
        var configuredSetupKeyHash = configuration[SetupKeyHashValidator.ConfigurationKey];
        return setupKeyValidator.Validate(configuredSetupKeyHash, setupKey);
    }

    public static bool TryMapSetupKeyValidationFailure(
        SetupKeyValidationOutcome outcome,
        out string message,
        out string? messageKey)
    {
        message = string.Empty;
        messageKey = null;

        switch (outcome)
        {
            case SetupKeyValidationOutcome.MissingHashConfiguration:
                message = "Setup key hash is not configured.";
                messageKey = SetupApiMessageKeys.Validation.SetupKeyHashNotConfigured;
                return false;
            case SetupKeyValidationOutcome.InvalidHashFormat:
                message = "Setup key hash format is invalid.";
                messageKey = SetupApiMessageKeys.Validation.SetupKeyHashInvalidFormat;
                return false;
            case SetupKeyValidationOutcome.InvalidKey:
                message = "Invalid setup key.";
                messageKey = SetupApiMessageKeys.Validation.InvalidSetupKey;
                return false;
            default:
                return true;
        }
    }

    private static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeUserName(string userName) =>
        userName.Trim().ToUpperInvariant();
}
