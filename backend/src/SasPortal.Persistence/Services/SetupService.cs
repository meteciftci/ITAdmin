using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;
using System.Security.Cryptography;
using System.Text;

namespace SasPortal.Persistence.Services;

public sealed class SetupService(
    AppDbContext context,
    ILdapService ldapService,
    ISecretProtector secretProtector,
    IConfiguration configuration,
    ILogger<SetupService> logger) : ISetupService
{
    private const string SuperAdminRoleCode = "SuperAdmin";
    private const string AdministratorRoleCode = "Administrator";
    private const string UserRoleCode = "User";
    private const string SetupActor = "setup";
    private const string ActiveDirectoryDirectorySource = "ActiveDirectory";
    private const string NationalIdApplicationSettingKey = "Directory:NationalIdAttribute";

    internal const string DirectoryUserProfileCouldNotBeLoadedMessage =
        "Directory user profile could not be loaded. Check the administrator user name, User Search Base, and User Search Filter.";

    private static readonly (string Module, string Code, string Description)[] DefaultPermissions =
    [
        ("Dashboard", "Dashboard.View", "View dashboard."),
        ("Users", "Users.View", "View users."),
        ("Users", "Users.Create", "Create users."),
        ("Users", "Users.Update", "Update users."),
        ("Users", "Users.Delete", "Delete users."),
        ("Users", "Users.AssignRoles", "Assign roles to users."),
        ("Roles", "Roles.View", "View roles."),
        ("Roles", "Roles.Create", "Create roles."),
        ("Roles", "Roles.Update", "Update roles."),
        ("Roles", "Roles.Delete", "Delete roles."),
        ("Roles", "Roles.AssignPermissions", "Assign permissions to roles."),
        ("Permissions", "Permissions.View", "View permissions."),
        ("AuditLogs", "AuditLogs.View", "View audit logs."),
        ("SecurityLogs", "SecurityLogs.View", "View security logs."),
        ("Settings", "Settings.View", "View settings."),
        ("Settings", "Settings.Update", "Update settings."),
        ("AdManagement", "AdManagement.Settings.View", "View AD management settings."),
        ("AdManagement", "AdManagement.Settings.Update", "Update AD management settings."),
        ("AdManagement", "AdManagement.Users.View", "View AD management directory users."),
        ("AdManagement", "AdManagement.Users.Create", "Create AD management directory users."),
        ("AdManagement", "AdManagement.Users.Update", "Update AD management directory users."),
        ("AdManagement", "AdManagement.Users.Enable", "Enable AD management directory user accounts."),
        ("AdManagement", "AdManagement.Users.Disable", "Disable AD management directory user accounts."),
        ("AdManagement", "AdManagement.Users.Unlock", "Unlock AD management directory user accounts."),
        ("AdManagement", "AdManagement.Users.Groups.View", "View AD user direct group memberships."),
        ("AdManagement", "AdManagement.Users.Groups.Add", "Add AD users to groups."),
        ("AdManagement", "AdManagement.Users.Groups.Remove", "Remove AD users from groups."),
        ("AdManagement", "AdManagement.Groups.View", "View AD management security groups."),
        ("AdManagement", "AdManagement.Groups.Create", "Create AD management security groups."),
        ("AdManagement", "AdManagement.Groups.Update", "Update AD management security groups."),
        ("AdManagement", "AdManagement.Groups.Delete", "Delete AD management security groups."),
        ("AdManagement", "AdManagement.Groups.ManageMembers", "Manage AD security group memberships."),
        ("AdManagement", "AdManagement.Users.MoveOu", "Move AD management directory users between OUs."),
        ("AdOperationLogs", "AdOperationLogs.View", "View AD operation logs."),
        ("NotificationProviders", "NotificationProviders.View", "View notification provider settings."),
        ("NotificationProviders", "NotificationProviders.Update", "Update notification provider settings."),
        ("NotificationProviders", "NotificationProviders.Test", "Send notification provider test messages."),
        ("NotificationOutbox", "NotificationOutbox.View", "View notification outbox."),
        ("NotificationOutbox", "NotificationOutbox.Retry", "Retry notification outbox items."),
        ("NotificationOutbox", "NotificationOutbox.Cancel", "Cancel notification outbox items."),
        ("NotificationTemplates", "NotificationTemplates.View", "View notification templates."),
        ("NotificationTemplates", "NotificationTemplates.Update", "Update notification templates."),
        ("Setup", "Setup.Manage", "Manage setup.")
    ];

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        var hasAnyUser = await context.PortalUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        var isSetupCompleted = await context.ApplicationSettings
            .AsNoTracking()
            .AnyAsync(x =>
                    x.Key == "Setup:IsCompleted" &&
                    x.Value == "true" &&
                    x.IsActive &&
                    !x.IsDeleted,
                cancellationToken);

        return !hasAnyUser || !isSetupCompleted;
    }

    public async Task<CompleteSetupResult> CompleteSetupAsync(
        CompleteSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SetupKey) ||
            string.IsNullOrWhiteSpace(request.Ldap.Host) ||
            string.IsNullOrWhiteSpace(request.Ldap.BaseDn) ||
            string.IsNullOrWhiteSpace(request.Ldap.UserSearchFilter) ||
            string.IsNullOrWhiteSpace(request.Ldap.BindUserName) ||
            string.IsNullOrWhiteSpace(request.Ldap.BindPassword) ||
            request.Ldap.Port <= 0 ||
            string.IsNullOrWhiteSpace(request.Admin.UserName) ||
            string.IsNullOrWhiteSpace(request.Admin.Password))
        {
            return new CompleteSetupResult(false, "Invalid setup request.");
        }

        var configuredSetupKey = configuration["Setup:SetupKey"];
        if (string.IsNullOrWhiteSpace(configuredSetupKey))
        {
            return new CompleteSetupResult(false, "Setup key is not configured.");
        }

        var requestSetupKeyBytes = Encoding.UTF8.GetBytes(request.SetupKey);
        var configuredSetupKeyBytes = Encoding.UTF8.GetBytes(configuredSetupKey);
        var isSetupKeyValid = requestSetupKeyBytes.Length == configuredSetupKeyBytes.Length &&
                              CryptographicOperations.FixedTimeEquals(requestSetupKeyBytes, configuredSetupKeyBytes);
        if (!isSetupKeyValid)
        {
            return new CompleteSetupResult(false, "Invalid setup key.");
        }

        var isSetupRequired = await IsSetupRequiredAsync(cancellationToken);
        if (!isSetupRequired)
        {
            return new CompleteSetupResult(false, "Setup has already been completed.");
        }

        logger.LogInformation("Setup complete started.");
        logger.LogInformation("Setup LDAP validation started.");

        var ldapResult = await ldapService.ValidateAsync(
            new LdapValidationRequest
            {
                Host = request.Ldap.Host,
                Port = request.Ldap.Port,
                UseSsl = request.Ldap.UseSsl,
                BaseDn = request.Ldap.BaseDn,
                UserSearchBase = request.Ldap.UserSearchBase,
                UserSearchFilter = request.Ldap.UserSearchFilter,
                BindUserName = request.Ldap.BindUserName,
                BindUserDomain = request.Ldap.BindUserDomain,
                BindPassword = request.Ldap.BindPassword,
                TestUserName = request.Admin.UserName,
                TestPassword = request.Admin.Password
            },
            cancellationToken);

        logger.LogInformation("Setup LDAP validation completed. IsValid={IsValid}", ldapResult.IsValid);

        if (!ldapResult.IsValid)
        {
            logger.LogWarning("Setup failed.");
            return new CompleteSetupResult(false, ldapResult.Message);
        }

        logger.LogInformation("Setup LDAP profile lookup started.");

        var nationalIdAttribute = string.IsNullOrWhiteSpace(request.Ldap.NationalIdAttribute)
            ? null
            : request.Ldap.NationalIdAttribute.Trim();

        LdapUserProfile? ldapProfile = null;
        foreach (var candidateUserName in BuildLdapUserNameCandidates(request.Admin.UserName))
        {
            ldapProfile = await ldapService.GetUserProfileAsync(
                new LdapUserProfileRequest(
                    Host: request.Ldap.Host,
                    Port: request.Ldap.Port,
                    UseSsl: request.Ldap.UseSsl,
                    BaseDn: request.Ldap.BaseDn,
                    UserSearchBase: request.Ldap.UserSearchBase,
                    UserSearchFilter: request.Ldap.UserSearchFilter,
                    BindUserName: request.Ldap.BindUserName,
                    BindUserDomain: request.Ldap.BindUserDomain,
                    BindPassword: request.Ldap.BindPassword,
                    UserName: candidateUserName,
                    NationalIdAttribute: nationalIdAttribute),
                cancellationToken);

            if (ldapProfile is not null)
            {
                break;
            }
        }

        logger.LogInformation("Setup LDAP profile lookup completed. Found={Found}", ldapProfile is not null);

        if (ldapProfile is null)
        {
            logger.LogWarning("Setup failed.");
            return new CompleteSetupResult(false, DirectoryUserProfileCouldNotBeLoadedMessage);
        }

        var usernameConflictExists = await context.PortalUsers.AnyAsync(
            x => x.UserName == ldapProfile.UserName &&
                 x.DirectoryObjectId != ldapProfile.DirectoryObjectId,
            cancellationToken);

        if (usernameConflictExists)
        {
            logger.LogWarning("Setup failed.");
            return new CompleteSetupResult(false, "A portal user with the same user name already exists.");
        }

        var displayName = ResolveDisplayName(ldapProfile);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        logger.LogInformation("Setup transaction started.");

        try
        {
            var now = DateTime.UtcNow;

            var activeLdapSetting = await context.LdapSettings
                .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);

            if (activeLdapSetting is null)
            {
                activeLdapSetting = new LdapSetting
                {
                    Name = string.IsNullOrWhiteSpace(request.Ldap.Name) ? "Default LDAP" : request.Ldap.Name,
                    Host = request.Ldap.Host,
                    Port = request.Ldap.Port,
                    UseSsl = request.Ldap.UseSsl,
                    BaseDn = request.Ldap.BaseDn,
                    UserSearchBase = request.Ldap.UserSearchBase,
                    UserSearchFilter = request.Ldap.UserSearchFilter,
                    BindUserName = request.Ldap.BindUserName,
                    BindUserDomain = request.Ldap.BindUserDomain,
                    EncryptedBindPassword = secretProtector.Protect(request.Ldap.BindPassword),
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = SetupActor
                };

                await context.LdapSettings.AddAsync(activeLdapSetting, cancellationToken);
            }
            else
            {
                activeLdapSetting.Name = string.IsNullOrWhiteSpace(request.Ldap.Name) ? "Default LDAP" : request.Ldap.Name;
                activeLdapSetting.Host = request.Ldap.Host;
                activeLdapSetting.Port = request.Ldap.Port;
                activeLdapSetting.UseSsl = request.Ldap.UseSsl;
                activeLdapSetting.BaseDn = request.Ldap.BaseDn;
                activeLdapSetting.UserSearchBase = request.Ldap.UserSearchBase;
                activeLdapSetting.UserSearchFilter = request.Ldap.UserSearchFilter;
                activeLdapSetting.BindUserName = request.Ldap.BindUserName;
                activeLdapSetting.BindUserDomain = request.Ldap.BindUserDomain;
                activeLdapSetting.EncryptedBindPassword = secretProtector.Protect(request.Ldap.BindPassword);
                activeLdapSetting.UpdatedAt = now;
                activeLdapSetting.UpdatedBy = SetupActor;
            }

            var superAdminRole = await context.PortalRoles
                .FirstOrDefaultAsync(x => x.Code == SuperAdminRoleCode, cancellationToken);
            if (superAdminRole is null)
            {
                superAdminRole = new PortalRole
                {
                    Name = "Super Admin",
                    Code = SuperAdminRoleCode,
                    Description = "Full system access role.",
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = SetupActor
                };
                await context.PortalRoles.AddAsync(superAdminRole, cancellationToken);
            }
            else
            {
                ApplySystemRoleMetadataIfChanged(
                    superAdminRole,
                    name: "Super Admin",
                    description: "Full system access role.",
                    now);
            }

            var administratorRole = await context.PortalRoles
                .FirstOrDefaultAsync(x => x.Code == AdministratorRoleCode, cancellationToken);
            if (administratorRole is null)
            {
                administratorRole = new PortalRole
                {
                    Name = "Administrator",
                    Code = AdministratorRoleCode,
                    Description = "System administrator role.",
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = SetupActor
                };
                await context.PortalRoles.AddAsync(administratorRole, cancellationToken);
            }
            else
            {
                ApplySystemRoleMetadataIfChanged(
                    administratorRole,
                    name: "Administrator",
                    description: "System administrator role.",
                    now);
            }

            var userRole = await context.PortalRoles
                .FirstOrDefaultAsync(x => x.Code == UserRoleCode, cancellationToken);
            if (userRole is null)
            {
                userRole = new PortalRole
                {
                    Name = "User",
                    Code = UserRoleCode,
                    Description = "Default user role.",
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = SetupActor
                };
                await context.PortalRoles.AddAsync(userRole, cancellationToken);
            }
            else
            {
                ApplySystemRoleMetadataIfChanged(
                    userRole,
                    name: "User",
                    description: "Default user role.",
                    now);
            }

            var allPermissions = new List<PortalPermission>(DefaultPermissions.Length);
            foreach (var defaultPermission in DefaultPermissions)
            {
                var permission = await context.PortalPermissions
                    .FirstOrDefaultAsync(x => x.Code == defaultPermission.Code, cancellationToken);

                if (permission is null)
                {
                    permission = new PortalPermission
                    {
                        Module = defaultPermission.Module,
                        Code = defaultPermission.Code,
                        Description = defaultPermission.Description,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = SetupActor
                    };
                    await context.PortalPermissions.AddAsync(permission, cancellationToken);
                }

                allPermissions.Add(permission);
            }

            foreach (var permission in allPermissions)
            {
                var hasRolePermission = await context.PortalRolePermissions
                    .AnyAsync(
                        x => x.PortalRoleId == administratorRole.Id &&
                             x.PortalPermissionId == permission.Id,
                        cancellationToken);

                if (!hasRolePermission)
                {
                    await context.PortalRolePermissions.AddAsync(
                        new PortalRolePermission
                        {
                            PortalRoleId = administratorRole.Id,
                            PortalPermissionId = permission.Id,
                            CreatedAt = now,
                            CreatedBy = SetupActor
                        },
                        cancellationToken);
                }
            }

            var dashboardPermission = allPermissions.First(x => x.Code == "Dashboard.View");
            var hasDashboardOnUserRole = await context.PortalRolePermissions
                .AnyAsync(
                    x => x.PortalRoleId == userRole.Id &&
                         x.PortalPermissionId == dashboardPermission.Id,
                    cancellationToken);

            if (!hasDashboardOnUserRole)
            {
                await context.PortalRolePermissions.AddAsync(
                    new PortalRolePermission
                    {
                        PortalRoleId = userRole.Id,
                        PortalPermissionId = dashboardPermission.Id,
                        CreatedAt = now,
                        CreatedBy = SetupActor
                    },
                    cancellationToken);
            }

            var adminUser = await context.PortalUsers
                .FirstOrDefaultAsync(x => x.DirectoryObjectId == ldapProfile.DirectoryObjectId, cancellationToken);

            if (adminUser is null)
            {
                adminUser = new PortalUser
                {
                    DirectorySource = ActiveDirectoryDirectorySource,
                    DirectoryObjectId = ldapProfile.DirectoryObjectId,
                    PreferredLanguage = "tr",
                    NationalIdEncrypted =
                        ldapProfile.NationalId is not null ? secretProtector.Protect(ldapProfile.NationalId) : null,
                    NationalIdMasked = ldapProfile.NationalId is not null ? MaskNationalId(ldapProfile.NationalId) : null,
                    UserName = ldapProfile.UserName,
                    DisplayName = displayName,
                    Email = ldapProfile.Email,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = SetupActor
                };
                await context.PortalUsers.AddAsync(adminUser, cancellationToken);
            }
            else
            {
                adminUser.DirectorySource = ActiveDirectoryDirectorySource;
                adminUser.DirectoryObjectId = ldapProfile.DirectoryObjectId;
                if (ldapProfile.NationalId is not null)
                {
                    adminUser.NationalIdEncrypted = secretProtector.Protect(ldapProfile.NationalId);
                    adminUser.NationalIdMasked = MaskNationalId(ldapProfile.NationalId);
                }

                adminUser.UserName = ldapProfile.UserName;
                adminUser.DisplayName = displayName;
                adminUser.Email = ldapProfile.Email;
                adminUser.IsActive = true;
                adminUser.UpdatedAt = now;
                adminUser.UpdatedBy = SetupActor;
            }

            if (!string.IsNullOrWhiteSpace(request.Ldap.NationalIdAttribute))
            {
                var nationalIdSetting =
                    await context.ApplicationSettings.FirstOrDefaultAsync(
                        x => x.Key == NationalIdApplicationSettingKey,
                        cancellationToken);

                if (nationalIdSetting is null)
                {
                    await context.ApplicationSettings.AddAsync(
                        new ApplicationSetting
                        {
                            Key = NationalIdApplicationSettingKey,
                            Value = request.Ldap.NationalIdAttribute.Trim(),
                            ValueType = SettingValueType.String,
                            Description = "LDAP attribute name that stores the national identity value.",
                            IsEncrypted = false,
                            IsSystem = true,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedBy = SetupActor
                        },
                        cancellationToken);
                }
                else
                {
                    nationalIdSetting.Value = request.Ldap.NationalIdAttribute.Trim();
                    nationalIdSetting.ValueType = SettingValueType.String;
                    nationalIdSetting.Description = "LDAP attribute name that stores the national identity value.";
                    nationalIdSetting.IsEncrypted = false;
                    nationalIdSetting.IsSystem = true;
                    nationalIdSetting.IsActive = true;
                    nationalIdSetting.UpdatedAt = now;
                    nationalIdSetting.UpdatedBy = SetupActor;
                }
            }

            var hasSuperAdminRole = await context.PortalUserRoles
                .AnyAsync(
                    x => x.PortalUserId == adminUser.Id &&
                         x.PortalRoleId == superAdminRole.Id,
                    cancellationToken);

            if (!hasSuperAdminRole)
            {
                await context.PortalUserRoles.AddAsync(
                    new PortalUserRole
                    {
                        PortalUserId = adminUser.Id,
                        PortalRoleId = superAdminRole.Id,
                        CreatedAt = now,
                        CreatedBy = SetupActor
                    },
                    cancellationToken);
            }

            var setupCompletionSetting = await context.ApplicationSettings
                .FirstOrDefaultAsync(x => x.Key == "Setup:IsCompleted", cancellationToken);

            if (setupCompletionSetting is null)
            {
                setupCompletionSetting = new ApplicationSetting
                {
                    Key = "Setup:IsCompleted",
                    Value = "true",
                    ValueType = SettingValueType.Boolean,
                    Description = "Indicates whether initial setup has been completed.",
                    IsEncrypted = false,
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = SetupActor
                };
                await context.ApplicationSettings.AddAsync(setupCompletionSetting, cancellationToken);
            }
            else
            {
                setupCompletionSetting.Value = "true";
                setupCompletionSetting.ValueType = SettingValueType.Boolean;
                setupCompletionSetting.Description = "Indicates whether initial setup has been completed.";
                setupCompletionSetting.IsEncrypted = false;
                setupCompletionSetting.IsSystem = true;
                setupCompletionSetting.IsActive = true;
                setupCompletionSetting.UpdatedAt = now;
                setupCompletionSetting.UpdatedBy = SetupActor;
            }

            await context.SecurityLogs.AddAsync(
                new SecurityLog
                {
                    UserId = adminUser.Id,
                    UserName = ldapProfile.UserName,
                    EventType = "SetupCompleted",
                    Severity = "Info",
                    Description = "Initial setup completed.",
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Setup completed successfully.");
            return new CompleteSetupResult(true, "Setup completed successfully.");
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception, "Setup failed.");
            return new CompleteSetupResult(false, "Setup could not be completed.");
        }
    }

    internal static IReadOnlyList<string> BuildLdapUserNameCandidates(string userName)
    {
        var trimmed = userName.Trim();
        if (trimmed.Length == 0)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        void AddCandidate(string candidate)
        {
            var c = candidate.Trim();
            if (c.Length == 0 || seen.Contains(c))
            {
                return;
            }

            seen.Add(c);
            result.Add(c);
        }

        AddCandidate(trimmed);

        var slashIdx = trimmed.LastIndexOf('\\');
        if (slashIdx >= 0 && slashIdx < trimmed.Length - 1)
        {
            AddCandidate(trimmed[(slashIdx + 1)..]);
        }

        var atIdx = trimmed.IndexOf('@');
        if (atIdx > 0)
        {
            AddCandidate(trimmed[..atIdx]);
        }

        return result;
    }

    private static string ResolveDisplayName(LdapUserProfile ldapProfile)
    {
        return string.IsNullOrWhiteSpace(ldapProfile.DisplayName)
            ? ldapProfile.UserName
            : ldapProfile.DisplayName;
    }

    private static string? MaskNationalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var length = trimmed.Length;

        string masked =
            length <= 4 ? new string('*', length)
            : $"{trimmed[..3]}{new string('*', length - 5)}{trimmed[^2..]}";

        return masked.Length > 50 ? masked[..50] : masked;
    }

    private static void ApplySystemRoleMetadataIfChanged(
        PortalRole role,
        string name,
        string description,
        DateTime now)
    {
        if (role.Name != name ||
            role.Description != description ||
            !role.IsSystem ||
            !role.IsActive)
        {
            role.Name = name;
            role.Description = description;
            role.IsSystem = true;
            role.IsActive = true;
            role.UpdatedAt = now;
            role.UpdatedBy = SetupActor;
        }
    }
}
