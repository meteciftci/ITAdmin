using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class PermissionService(AppDbContext context) : IPermissionService
{
    public async Task<PagedResult<PermissionListItem>> GetPermissionsAsync(
        PermissionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        IQueryable<Domain.Entities.PortalPermission> permissionsQuery = context.PortalPermissions
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            permissionsQuery = permissionsQuery.Where(x =>
                EF.Functions.ILike(x.Code, pattern)
                || EF.Functions.ILike(x.Module, pattern)
                || (x.Description != null && EF.Functions.ILike(x.Description, pattern))
                || EF.Functions.ILike(x.Code.Replace(".", " "), pattern));
        }

        if (query.IsActive is { } isActive)
        {
            permissionsQuery = permissionsQuery.Where(x => x.IsActive == isActive);
        }

        var totalCount = await permissionsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await permissionsQuery
            .OrderBy(x => x.Module)
            .ThenBy(x => x.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PermissionListItem(
                x.Id,
                x.Module,
                PermissionNameFromCode(x.Code),
                x.Code,
                x.Description,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<PermissionListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<PermissionDetail?> GetPermissionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var permission = await context.PortalPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (permission is null)
        {
            return null;
        }

        return new PermissionDetail(
            permission.Id,
            permission.Module,
            PermissionNameFromCode(permission.Code),
            permission.Code,
            permission.Description,
            permission.IsActive,
            permission.CreatedAt,
            permission.CreatedBy,
            permission.UpdatedAt,
            permission.UpdatedBy);
    }

    private static string BuildILikeContainsPattern(string search)
    {
        var trimmed = search.Trim();
        return $"%{trimmed}%";
    }

    private static string PermissionNameFromCode(string code) => code.Replace('.', ' ');
}
