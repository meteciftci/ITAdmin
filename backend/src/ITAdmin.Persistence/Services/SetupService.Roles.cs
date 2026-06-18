using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed partial class SetupService
{
    private static readonly (string Module, string Code, string Description)[] DefaultPermissions =
    [
        ("Dashboard", PermissionCodes.Dashboard.View, "View dashboard."),
        ("Users", PermissionCodes.Users.View, "View users."),
        ("Users", PermissionCodes.Users.Create, "Create users."),
        ("Users", PermissionCodes.Users.Update, "Update users."),
        ("Users", PermissionCodes.Users.Delete, "Delete users."),
        ("Users", PermissionCodes.Users.AssignRoles, "Assign roles to users."),
        ("Roles", PermissionCodes.Roles.View, "View roles."),
        ("Roles", PermissionCodes.Roles.Create, "Create roles."),
        ("Roles", PermissionCodes.Roles.Update, "Update roles."),
        ("Roles", PermissionCodes.Roles.Delete, "Delete roles."),
        ("Roles", PermissionCodes.Roles.AssignPermissions, "Assign permissions to roles."),
        ("Permissions", PermissionCodes.Permissions.View, "View permissions."),
        ("AuditLogs", PermissionCodes.AuditLogs.View, "View audit logs."),
        ("SecurityLogs", PermissionCodes.SecurityLogs.View, "View security logs."),
        ("Settings", PermissionCodes.Settings.View, "View settings."),
        ("Settings", PermissionCodes.Settings.Update, "Update settings."),
        ("AdManagement", PermissionCodes.AdManagement.Settings.View, "View AD management settings."),
        ("AdManagement", PermissionCodes.AdManagement.Settings.Update, "Update AD management settings."),
        ("AdManagement", PermissionCodes.AdManagement.Users.View, "View AD management directory users."),
        ("AdManagement", PermissionCodes.AdManagement.Users.Create, "Create AD management directory users."),
        ("AdManagement", PermissionCodes.AdManagement.Users.Update, "Update AD management directory users."),
        ("AdManagement", PermissionCodes.AdManagement.Users.Enable, "Enable AD management directory user accounts."),
        ("AdManagement", PermissionCodes.AdManagement.Users.Disable, "Disable AD management directory user accounts."),
        ("AdManagement", PermissionCodes.AdManagement.Users.Unlock, "Unlock AD management directory user accounts."),
        ("AdManagement", PermissionCodes.AdManagement.Users.Groups.View, "View AD user direct group memberships."),
        ("AdManagement", PermissionCodes.AdManagement.Users.Groups.Add, "Add AD users to groups."),
        ("AdManagement", PermissionCodes.AdManagement.Users.Groups.Remove, "Remove AD users from groups."),
        ("AdManagement", PermissionCodes.AdManagement.Groups.View, "View AD management security groups."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.View, "View AD management directory computers."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.Update, "Update AD management directory computer attributes."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.MoveOu, "Move AD management directory computers between OUs."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.Enable, "Enable AD management directory computer accounts."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.Disable, "Disable AD management directory computer accounts."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.Delete, "Delete AD management directory computer accounts."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.Groups.View, "View AD computer direct group memberships."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.Groups.Add, "Add AD computers to groups."),
        ("AdManagement", PermissionCodes.AdManagement.Computers.Groups.Remove, "Remove AD computers from groups."),
        ("AdManagement", PermissionCodes.AdManagement.DeletedObjects.View, "View AD management deleted directory objects."),
        ("AdManagement", PermissionCodes.AdManagement.DeletedObjects.Restore, "Restore AD management deleted directory objects."),
        ("AdManagement", PermissionCodes.AdManagement.OrganizationalUnits.View, "View AD management organizational units."),
        ("AdManagement", PermissionCodes.AdManagement.OrganizationalUnits.Create, "Create AD management organizational units."),
        ("AdManagement", PermissionCodes.AdManagement.OrganizationalUnits.Update, "Rename AD management organizational units."),
        ("AdManagement", PermissionCodes.AdManagement.OrganizationalUnits.Move, "Move AD management organizational units."),
        ("AdManagement", PermissionCodes.AdManagement.OrganizationalUnits.Delete, "Delete AD management organizational units."),
        ("AdManagement", PermissionCodes.AdManagement.Groups.Create, "Create AD management security groups."),
        ("AdManagement", PermissionCodes.AdManagement.Groups.Update, "Update AD management security groups."),
        ("AdManagement", PermissionCodes.AdManagement.Groups.Delete, "Delete AD management security groups."),
        ("AdManagement", PermissionCodes.AdManagement.Groups.ManageMembers, "Manage AD security group memberships."),
        ("AdManagement", PermissionCodes.AdManagement.Groups.MoveOu, "Move AD management security groups between OUs."),
        ("AdManagement", PermissionCodes.AdManagement.Users.MoveOu, "Move AD management directory users between OUs."),
        ("AdOperationLogs", PermissionCodes.AdOperationLogs.View, "View AD operation logs."),
        ("NotificationProviders", PermissionCodes.NotificationProviders.View, "View notification provider settings."),
        ("NotificationProviders", PermissionCodes.NotificationProviders.Update, "Update notification provider settings."),
        ("NotificationProviders", PermissionCodes.NotificationProviders.Test, "Send notification provider test messages."),
        ("NotificationOutbox", PermissionCodes.NotificationOutbox.View, "View notification outbox."),
        ("NotificationOutbox", PermissionCodes.NotificationOutbox.Retry, "Retry notification outbox items."),
        ("NotificationOutbox", PermissionCodes.NotificationOutbox.Cancel, "Cancel notification outbox items."),
        ("NotificationTemplates", PermissionCodes.NotificationTemplates.View, "View notification templates."),
        ("NotificationTemplates", PermissionCodes.NotificationTemplates.Update, "Update notification templates."),
        ("Setup", PermissionCodes.Setup.Manage, "Manage setup.")
    ];

    private async Task<PortalRole> EnsureDefaultRolesAndPermissionsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var superAdminRole = await EnsureSystemRoleAsync(
            SuperAdminRoleCode,
            "Super Admin",
            "Full system access role.",
            now,
            cancellationToken);

        var administratorRole = await EnsureSystemRoleAsync(
            AdministratorRoleCode,
            "Administrator",
            "System administrator role.",
            now,
            cancellationToken);

        var userRole = await EnsureSystemRoleAsync(
            UserRoleCode,
            "User",
            "Default user role.",
            now,
            cancellationToken);

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

        return superAdminRole;
    }

    private async Task<PortalRole> EnsureSystemRoleAsync(
        string code,
        string name,
        string description,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var role = await context.PortalRoles
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

        if (role is null)
        {
            role = new PortalRole
            {
                Name = name,
                Code = code,
                Description = description,
                IsSystem = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = SetupActor
            };
            await context.PortalRoles.AddAsync(role, cancellationToken);
            return role;
        }

        ApplySystemRoleMetadataIfChanged(role, name, description, now);
        return role;
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
