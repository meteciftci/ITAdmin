using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Application.Common.Security;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class RoleService(AppDbContext context) : IRoleService
{
    private static readonly JsonSerializerOptions AuditJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

            var newValuesPayload = JsonSerializer.Serialize(
                new
                {
                    summary = "Portal role created.",
                    name = role.Name,
                    code = role.Code,
                    isActive = role.IsActive,
                    isSystem = role.IsSystem
                },
                AuditJsonSerializerOptions);

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = AuditActionType.Create,
                    EntityName = "PortalRole",
                    EntityId = role.Id.ToString(),
                    UserName = request.ActorUserName,
                    NewValues = newValuesPayload,
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new CreateRoleResult(true, string.Empty, MapRoleDetail(role));
        }
        catch (DbUpdateException ex) when (IsUniqueRoleCodeViolation(ex))
        {
            return new CreateRoleResult(false, "A role with the same code already exists.", null);
        }
        catch
        {
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

            var oldValuesPayload = JsonSerializer.Serialize(
                new
                {
                    name = role.Name,
                    description = role.Description,
                    isActive = role.IsActive
                },
                AuditJsonSerializerOptions);

            var now = DateTime.UtcNow;
            role.Name = normalizedName;
            role.Description = normalizedDescription;
            role.IsActive = request.IsActive;
            role.UpdatedAt = now;
            role.UpdatedBy = request.ActorUserName ?? "system";

            var newValuesPayload = JsonSerializer.Serialize(
                new
                {
                    summary = "Portal role updated.",
                    name = role.Name,
                    description = role.Description,
                    isActive = role.IsActive
                },
                AuditJsonSerializerOptions);

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = AuditActionType.Update,
                    EntityName = "PortalRole",
                    EntityId = role.Id.ToString(),
                    UserName = request.ActorUserName,
                    OldValues = oldValuesPayload,
                    NewValues = newValuesPayload,
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new UpdateRoleResult(true, string.Empty, MapRoleDetail(role));
        }
        catch
        {
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

            var oldValuesPayload = JsonSerializer.Serialize(
                new
                {
                    name = role.Name,
                    code = role.Code,
                    isActive = role.IsActive
                },
                AuditJsonSerializerOptions);

            var now = DateTime.UtcNow;
            role.IsActive = request.IsActive;
            role.UpdatedAt = now;
            role.UpdatedBy = request.ActorUserName ?? "system";

            var summary = request.IsActive
                ? "Portal role activated."
                : "Portal role deactivated.";

            var newValuesPayload = JsonSerializer.Serialize(
                new
                {
                    summary,
                    name = role.Name,
                    code = role.Code,
                    isActive = role.IsActive
                },
                AuditJsonSerializerOptions);

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = AuditActionType.Update,
                    EntityName = "PortalRole",
                    EntityId = role.Id.ToString(),
                    UserName = request.ActorUserName,
                    OldValues = oldValuesPayload,
                    NewValues = newValuesPayload,
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new UpdateRoleStatusResult(true, string.Empty, MapRoleDetail(role));
        }
        catch
        {
            return new UpdateRoleStatusResult(false, "Role status could not be updated.", null);
        }
    }

    private static string BuildILikeContainsPattern(string search)
    {
        var trimmed = search.Trim();
        return $"%{trimmed}%";
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
