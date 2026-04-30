using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    IConfiguration configuration) : ISetupService
{
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
            string.IsNullOrWhiteSpace(request.Admin.Password) ||
            string.IsNullOrWhiteSpace(request.Admin.DisplayName))
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

        if (!ldapResult.IsValid)
        {
            return new CompleteSetupResult(false, ldapResult.Message);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

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
                    CreatedBy = "setup"
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
                activeLdapSetting.UpdatedBy = "setup";
            }

            var administratorRole = await context.PortalRoles
                .FirstOrDefaultAsync(x => x.Code == "Administrator", cancellationToken);
            if (administratorRole is null)
            {
                administratorRole = new PortalRole
                {
                    Name = "Administrator",
                    Code = "Administrator",
                    Description = "System administrator role.",
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "setup"
                };
                await context.PortalRoles.AddAsync(administratorRole, cancellationToken);
            }

            var userRole = await context.PortalRoles
                .FirstOrDefaultAsync(x => x.Code == "User", cancellationToken);
            if (userRole is null)
            {
                userRole = new PortalRole
                {
                    Name = "User",
                    Code = "User",
                    Description = "Default user role.",
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "setup"
                };
                await context.PortalRoles.AddAsync(userRole, cancellationToken);
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
                        CreatedBy = "setup"
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
                            CreatedBy = "setup"
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
                        CreatedBy = "setup"
                    },
                    cancellationToken);
            }

            var adminUser = await context.PortalUsers
                .FirstOrDefaultAsync(x => x.UserName == request.Admin.UserName, cancellationToken);

            if (adminUser is null)
            {
                adminUser = new PortalUser
                {
                    UserName = request.Admin.UserName,
                    DisplayName = request.Admin.DisplayName,
                    Email = request.Admin.Email,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "setup"
                };
                await context.PortalUsers.AddAsync(adminUser, cancellationToken);
            }
            else
            {
                adminUser.DisplayName = request.Admin.DisplayName;
                adminUser.Email = request.Admin.Email;
                adminUser.IsActive = true;
                adminUser.UpdatedAt = now;
                adminUser.UpdatedBy = "setup";
            }

            var hasAdminRole = await context.PortalUserRoles
                .AnyAsync(
                    x => x.PortalUserId == adminUser.Id &&
                         x.PortalRoleId == administratorRole.Id,
                    cancellationToken);

            if (!hasAdminRole)
            {
                await context.PortalUserRoles.AddAsync(
                    new PortalUserRole
                    {
                        PortalUserId = adminUser.Id,
                        PortalRoleId = administratorRole.Id,
                        CreatedAt = now,
                        CreatedBy = "setup"
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
                    CreatedBy = "setup"
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
                setupCompletionSetting.UpdatedBy = "setup";
            }

            await context.SecurityLogs.AddAsync(
                new SecurityLog
                {
                    PortalUserId = adminUser.Id,
                    UserName = request.Admin.UserName,
                    EventType = SecurityEventType.SetupCompleted,
                    IsSuccess = true,
                    Message = "Initial setup completed.",
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CompleteSetupResult(true, "Setup completed successfully.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CompleteSetupResult(false, "Setup could not be completed.");
        }
    }
}
