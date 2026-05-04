using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class UserService(AppDbContext context) : IUserService
{
    public async Task<PagedResult<UserListItem>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        IQueryable<PortalUser> usersQuery = context.PortalUsers
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (query.IsActive is { } isActive)
        {
            usersQuery = usersQuery.Where(x => x.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            usersQuery = usersQuery.Where(x =>
                EF.Functions.ILike(x.UserName, pattern)
                || EF.Functions.ILike(x.DisplayName, pattern)
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern)));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await usersQuery
            .Include(x => x.UserRoles)
                .ThenInclude(ur => ur.PortalRole)
            .OrderBy(x => x.UserName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var listItems = items
            .Select(MapToListItem)
            .ToList();

        return new PagedResult<UserListItem>(listItems, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<UserDetail?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await context.PortalUsers
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.UserRoles)
                .ThenInclude(ur => ur.PortalRole)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        return MapToDetail(user);
    }

    private static UserListItem MapToListItem(PortalUser user)
    {
        var roles = GetActiveRoleCodes(user);
        return new UserListItem(
            user.Id,
            user.DirectorySource,
            user.DirectoryObjectId,
            user.UserName,
            user.DisplayName,
            user.NationalIdMasked,
            user.Email,
            user.IsActive,
            user.LastLoginAt,
            roles);
    }

    private static UserDetail MapToDetail(PortalUser user)
    {
        var roles = GetActiveRoleCodes(user);
        return new UserDetail(
            user.Id,
            user.DirectorySource,
            user.DirectoryObjectId,
            user.UserName,
            user.DisplayName,
            user.NationalIdMasked,
            user.Email,
            user.IsActive,
            user.LastLoginAt,
            roles,
            user.CreatedAt,
            user.CreatedBy,
            user.UpdatedAt,
            user.UpdatedBy);
    }

    private static List<string> GetActiveRoleCodes(PortalUser user) =>
        user.UserRoles
            .Where(ur => ur.PortalRole.IsActive && !ur.PortalRole.IsDeleted)
            .Select(ur => ur.PortalRole.Code)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string BuildILikeContainsPattern(string search)
    {
        var trimmed = search.Trim();
        return $"%{trimmed}%";
    }
}
