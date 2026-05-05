using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class RoleService(AppDbContext context) : IRoleService
{
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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

    private static string BuildILikeContainsPattern(string search)
    {
        var trimmed = search.Trim();
        return $"%{trimmed}%";
    }

    private static string PermissionNameFromCode(string code) => code.Replace('.', ' ');
}
