using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class RoleService(AppDbContext context, ILogger<RoleService> logger) : IRoleService
{
    private const int AuditDescriptionMaxLength = 2000;
    private const int AuditIpAddressMaxLength = 64;
    private const int AuditUserAgentMaxLength = 1024;

    public async Task<PagedResult<RoleListItem>> GetRolesAsync(RoleListQuery query, CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        IQueryable<PortalRole> rolesQuery = context.PortalRoles
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            rolesQuery = rolesQuery.Where(x =>
                EF.Functions.ILike(x.Name, pattern)
                || EF.Functions.ILike(x.Code, pattern)
                || (x.Description != null && EF.Functions.ILike(x.Description, pattern)));
        }

        if (query.IsActive is { } isActive)
        {
            rolesQuery = rolesQuery.Where(x => x.IsActive == isActive);
        }

        if (query.IsSystem is { } isSystem)
        {
            rolesQuery = rolesQuery.Where(x => x.IsSystem == isSystem);
        }

        var totalCount = await rolesQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await rolesQuery
            .OrderBy(x => x.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RoleListItem(
                x.Id,
                x.Name,
                x.Code,
                x.Description,
                x.IsSystem,
                x.IsActive,
                x.RolePermissions.Count(rp =>
                    rp.PortalPermission.IsActive &&
                    !rp.PortalPermission.IsDeleted)))
            .ToListAsync(cancellationToken);

        return new PagedResult<RoleListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<RoleDetail?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await context.PortalRoles
            .AsNoTracking()
            .Include(x => x.RolePermissions)
                .ThenInclude(rp => rp.PortalPermission)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (role is null)
        {
            return null;
        }

        var permissions = role.RolePermissions
            .Where(x => x.PortalPermission.IsActive && !x.PortalPermission.IsDeleted)
            .Select(x => x.PortalPermission)
            .OrderBy(x => x.Code)
            .Select(x => new RolePermissionItem(
                x.Id,
                PermissionNameFromCode(x.Code),
                x.Code,
                x.Description,
                x.IsActive))
            .ToList();

        return new RoleDetail(
            role.Id,
            role.Name,
            role.Code,
            role.Description,
            role.IsSystem,
            role.IsActive,
            permissions,
            role.CreatedAt,
            role.CreatedBy,
            role.UpdatedAt,
            role.UpdatedBy);
    }

    public async Task<CreateRoleResult> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new CreateRoleResult(false, "Role name is required.", null);
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return new CreateRoleResult(false, "Role code is required.", null);
            }

            var normalizedCode = NormalizeRoleCode(request.Code);
            if (!ValidateRoleCode(normalizedCode))
            {
                return new CreateRoleResult(false, "Role code format is invalid.", null);
            }

            var normalizedName = request.Name.Trim();
            var normalizedDescription = NormalizeDescription(request.Description);

            if (normalizedName.Length > 100)
            {
                return new CreateRoleResult(false, "Role name length is invalid.", null);
            }

            if (normalizedDescription is not null && normalizedDescription.Length > 500)
            {
                return new CreateRoleResult(false, "Role description length is invalid.", null);
            }

            var hasSameCode = await context.PortalRoles
                .AnyAsync(x => EF.Functions.ILike(x.Code, normalizedCode), cancellationToken);
            if (hasSameCode)
            {
                return new CreateRoleResult(false, "A role with the same code already exists.", null);
            }

            var now = DateTime.UtcNow;
            var role = new PortalRole
            {
                Name = normalizedName,
                Code = normalizedCode,
                Description = normalizedDescription,
                IsSystem = false,
                IsActive = request.IsActive,
                CreatedAt = now,
                CreatedBy = request.ActorUserName ?? "system"
            };

            await context.PortalRoles.AddAsync(role, cancellationToken);

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Create",
                    EntityName = "PortalRole",
                    EntityId = role.Id.ToString(),
                    Description = BuildCreateRoleAuditDescription(role),
                    ActorUserName = request.ActorUserName,
                    IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                    UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new CreateRoleResult(true, string.Empty, MapRoleDetail(role));
        }
        catch (DbUpdateException ex) when (IsUniqueRoleCodeViolation(ex))
        {
            return new CreateRoleResult(false, "A role with the same code already exists.", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Role creation failed.");
            return new CreateRoleResult(false, "Role could not be created.", null);
        }
    }

    public async Task<UpdateRoleResult> UpdateRoleAsync(
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var role = await context.PortalRoles
                .Where(x => !x.IsDeleted && x.Id == request.RoleId)
                .Include(x => x.RolePermissions)
                    .ThenInclude(rp => rp.PortalPermission)
                .FirstOrDefaultAsync(cancellationToken);

            if (role is null)
            {
                return new UpdateRoleResult(false, "Role was not found.", null);
            }

            if (IsSystemRole(role))
            {
                return new UpdateRoleResult(false, "System roles cannot be updated.", null);
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new UpdateRoleResult(false, "Role name is required.", null);
            }

            var normalizedName = request.Name.Trim();
            var normalizedDescription = NormalizeDescription(request.Description);

            if (normalizedName.Length > 100)
            {
                return new UpdateRoleResult(false, "Role name length is invalid.", null);
            }

            if (normalizedDescription is not null && normalizedDescription.Length > 500)
            {
                return new UpdateRoleResult(false, "Role description length is invalid.", null);
            }

            var oldName = role.Name;
            var oldDescription = role.Description;
            var oldStatus = role.IsActive;
            var now = DateTime.UtcNow;
            role.Name = normalizedName;
            role.Description = normalizedDescription;
            role.IsActive = request.IsActive;
            role.UpdatedAt = now;
            role.UpdatedBy = request.ActorUserName ?? "system";

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Update",
                    EntityName = "PortalRole",
                    EntityId = role.Id.ToString(),
                    Description = BuildUpdateRoleAuditDescription(
                        oldName,
                        oldDescription,
                        oldStatus,
                        role),
                    ActorUserName = request.ActorUserName,
                    IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                    UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new UpdateRoleResult(true, string.Empty, MapRoleDetail(role));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Role update failed for role {RoleId}.", request.RoleId);
            return new UpdateRoleResult(false, "Role could not be updated.", null);
        }
    }

    public async Task<UpdateRoleStatusResult> UpdateRoleStatusAsync(
        UpdateRoleStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var role = await context.PortalRoles
                .Where(x => !x.IsDeleted && x.Id == request.RoleId)
                .Include(x => x.RolePermissions)
                    .ThenInclude(rp => rp.PortalPermission)
                .FirstOrDefaultAsync(cancellationToken);

            if (role is null)
            {
                return new UpdateRoleStatusResult(false, "Role was not found.", null);
            }

            if (IsSystemRole(role))
            {
                return new UpdateRoleStatusResult(
                    false,
                    "System roles cannot be deactivated or activated from this endpoint.",
                    null);
            }

            var now = DateTime.UtcNow;
            var oldStatus = role.IsActive;
            role.IsActive = request.IsActive;
            role.UpdatedAt = now;
            role.UpdatedBy = request.ActorUserName ?? "system";

            var summary = request.IsActive
                ? $"Portal role activated: {FormatRoleIdentity(role)}. Status: {FormatStatus(oldStatus)} -> {FormatStatus(request.IsActive)}."
                : $"Portal role deactivated: {FormatRoleIdentity(role)}. Status: {FormatStatus(oldStatus)} -> {FormatStatus(request.IsActive)}.";

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Update",
                    EntityName = "PortalRole",
                    EntityId = role.Id.ToString(),
                    Description = TruncateAuditDescription(summary),
                    ActorUserName = request.ActorUserName,
                    IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                    UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new UpdateRoleStatusResult(true, string.Empty, MapRoleDetail(role));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Role status update failed for role {RoleId}.", request.RoleId);
            return new UpdateRoleStatusResult(false, "Role status could not be updated.", null);
        }
    }

    public async Task<UpdateRolePermissionsResult> UpdateRolePermissionsAsync(
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.PermissionIds is null)
            {
                return new UpdateRolePermissionsResult(false, "Permission ids are required.", null);
            }

            var permissionIds = request.PermissionIds.Distinct().ToList();
            var role = await context.PortalRoles
                .Where(x => !x.IsDeleted && x.Id == request.RoleId)
                .Include(x => x.RolePermissions)
                    .ThenInclude(rp => rp.PortalPermission)
                .FirstOrDefaultAsync(cancellationToken);

            if (role is null)
            {
                return new UpdateRolePermissionsResult(false, "Role was not found.", null);
            }

            if (IsSystemRole(role))
            {
                return new UpdateRolePermissionsResult(
                    false,
                    "System role permissions cannot be changed from this endpoint.",
                    null);
            }

            var now = DateTime.UtcNow;
            var actor = request.ActorUserName ?? "system";
            var currentPermissionCodes = role.RolePermissions
                .Where(rp => rp.PortalPermission.IsActive && !rp.PortalPermission.IsDeleted)
                .Select(rp => rp.PortalPermission.Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            if (permissionIds.Count == 0)
            {
                context.PortalRolePermissions.RemoveRange(role.RolePermissions);

                role.UpdatedAt = now;
                role.UpdatedBy = actor;
                var removedPermissionCodesOnClear = currentPermissionCodes;

                await context.AuditLogs.AddAsync(
                    new AuditLog
                    {
                        Action = "Update",
                        EntityName = "PortalRole",
                        EntityId = role.Id.ToString(),
                        Description = BuildUpdateRolePermissionsAuditDescription(
                            role,
                            Array.Empty<string>(),
                            removedPermissionCodesOnClear),
                        ActorUserName = request.ActorUserName,
                        IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                        UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                        CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);

                return new UpdateRolePermissionsResult(true, string.Empty, MapRoleDetail(role));
            }

            var permissions = await context.PortalPermissions
                .Where(x => !x.IsDeleted && permissionIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (permissions.Count != permissionIds.Count)
            {
                return new UpdateRolePermissionsResult(false, "One or more permissions were not found.", null);
            }

            if (permissions.Any(x => !x.IsActive))
            {
                return new UpdateRolePermissionsResult(false, "One or more permissions are inactive.", null);
            }

            var requestedPermissionCodes = permissions
                .Select(p => p.Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            var addedPermissionCodes = requestedPermissionCodes
                .Except(currentPermissionCodes, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            var removedPermissionCodes = currentPermissionCodes
                .Except(requestedPermissionCodes, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            var existingPermissionIds = role.RolePermissions
                .Select(x => x.PortalPermissionId)
                .ToHashSet();

            var requestedPermissionIds = permissionIds.ToHashSet();

            var rolePermissionsToRemove = role.RolePermissions
                .Where(x => !requestedPermissionIds.Contains(x.PortalPermissionId))
                .ToList();

            if (rolePermissionsToRemove.Count > 0)
            {
                context.PortalRolePermissions.RemoveRange(rolePermissionsToRemove);
            }

            var permissionsToAdd = permissionIds
                .Where(permissionId => !existingPermissionIds.Contains(permissionId))
                .Select(permissionId => new PortalRolePermission
                {
                    PortalRoleId = role.Id,
                    PortalPermissionId = permissionId,
                    CreatedAt = now,
                    CreatedBy = actor
                })
                .ToList();

            if (permissionsToAdd.Count > 0)
            {
                await context.PortalRolePermissions.AddRangeAsync(permissionsToAdd, cancellationToken);
            }

            role.UpdatedAt = now;
            role.UpdatedBy = actor;

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Update",
                    EntityName = "PortalRole",
                    EntityId = role.Id.ToString(),
                    Description = BuildUpdateRolePermissionsAuditDescription(role, addedPermissionCodes, removedPermissionCodes),
                    ActorUserName = request.ActorUserName,
                    IpAddress = TruncateAuditIpAddress(request.ActorIpAddress),
                    UserAgent = TruncateAuditUserAgent(request.ActorUserAgent),
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            await context.Entry(role)
                .Collection(x => x.RolePermissions)
                .Query()
                .Include(x => x.PortalPermission)
                .LoadAsync(cancellationToken);

            return new UpdateRolePermissionsResult(true, string.Empty, MapRoleDetail(role));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Role permissions update failed for role {RoleId}.", request.RoleId);
            return new UpdateRolePermissionsResult(false, "Role permissions could not be updated.", null);
        }
    }

    private static string BuildILikeContainsPattern(string search)
    {
        var trimmed = search.Trim();
        return $"%{trimmed}%";
    }

    private static string BuildCreateRoleAuditDescription(PortalRole role)
    {
        var summary = $"Portal role created: {FormatRoleIdentity(role)}. Status: {FormatStatus(role.IsActive)}.";
        if (!string.IsNullOrWhiteSpace(role.Description))
        {
            summary += " Description provided.";
        }

        return TruncateAuditDescription(summary);
    }

    private static string BuildUpdateRoleAuditDescription(
        string oldName,
        string? oldDescription,
        bool oldStatus,
        PortalRole role)
    {
        var summary = $"Portal role updated: {FormatRoleIdentity(role)}.";
        var changes = new List<string>();

        if (!string.Equals(oldName, role.Name, StringComparison.Ordinal))
        {
            changes.Add($"Name: \"{oldName}\" -> \"{role.Name}\".");
        }

        if (!string.Equals(oldDescription, role.Description, StringComparison.Ordinal))
        {
            changes.Add("Description changed.");
        }

        if (oldStatus != role.IsActive)
        {
            changes.Add($"Status: {FormatStatus(oldStatus)} -> {FormatStatus(role.IsActive)}.");
        }

        if (changes.Count == 0)
        {
            return TruncateAuditDescription($"{summary} No field changes.");
        }

        return TruncateAuditDescription($"{summary} {string.Join(" ", changes)}");
    }

    private static string BuildUpdateRolePermissionsAuditDescription(
        PortalRole role,
        IReadOnlyList<string> addedPermissionCodes,
        IReadOnlyList<string> removedPermissionCodes)
    {
        var summary = $"Portal role permissions updated: {FormatRoleIdentity(role)}.";
        var addedText = FormatChangedList("Added permissions", addedPermissionCodes);
        var removedText = FormatChangedList("Removed permissions", removedPermissionCodes);

        if (addedText is null && removedText is null)
        {
            return TruncateAuditDescription($"{summary} No permission changes.");
        }

        if (addedText is not null)
        {
            summary += $" {addedText}";
        }

        if (removedText is not null)
        {
            summary += $" {removedText}";
        }

        return TruncateAuditDescription(summary);
    }

    private static string FormatRoleIdentity(PortalRole role) => $"{role.Name} ({role.Code})";

    private static string? FormatChangedList(string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        return $"{label}: {string.Join(", ", values)}.";
    }

    private static string FormatStatus(bool isActive) => isActive ? "Active" : "Passive";

    private static string TruncateAuditDescription(string description) =>
        description.Length <= AuditDescriptionMaxLength
            ? description
            : $"{description[..(AuditDescriptionMaxLength - 3)]}...";

    private static string? TruncateAuditIpAddress(string? ipAddress) =>
        TruncateNullable(ipAddress, AuditIpAddressMaxLength);

    private static string? TruncateAuditUserAgent(string? userAgent) =>
        TruncateNullable(userAgent, AuditUserAgentMaxLength);

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static bool ValidateRoleCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (code.Length is < 2 or > 100)
        {
            return false;
        }

        return Regex.IsMatch(code, "^[A-Za-z0-9._-]+$");
    }

    private static string NormalizeRoleCode(string code) => code.Trim();

    private static bool IsUniqueRoleCodeViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg &&
        pg.SqlState == PostgresErrorCodes.UniqueViolation;

    private static string? NormalizeDescription(string? description)
    {
        if (description is null)
        {
            return null;
        }

        var trimmed = description.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool IsSystemRoleCode(string code) =>
        string.Equals(code, SystemRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, SystemRoles.Administrator, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, SystemRoles.User, StringComparison.OrdinalIgnoreCase);

    private static bool IsSystemRole(PortalRole role) => role.IsSystem || IsSystemRoleCode(role.Code);

    private static RoleDetail MapRoleDetail(PortalRole role)
    {
        var permissions = role.RolePermissions
            .Where(x => x.PortalPermission.IsActive && !x.PortalPermission.IsDeleted)
            .Select(x => x.PortalPermission)
            .OrderBy(x => x.Code)
            .Select(x => new RolePermissionItem(
                x.Id,
                PermissionNameFromCode(x.Code),
                x.Code,
                x.Description,
                x.IsActive))
            .ToList();

        return new RoleDetail(
            role.Id,
            role.Name,
            role.Code,
            role.Description,
            role.IsSystem,
            role.IsActive,
            permissions,
            role.CreatedAt,
            role.CreatedBy,
            role.UpdatedAt,
            role.UpdatedBy);
    }

    private static string PermissionNameFromCode(string code) => code.Replace('.', ' ');
}
