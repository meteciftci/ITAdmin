using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class UserService(
    AppDbContext context,
    ILdapService ldapService,
    ISecretProtector secretProtector) : IUserService
{
    private const string NationalIdApplicationSettingKey = "Directory:NationalIdAttribute";

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

    public async Task<UserDirectoryLookupResult> LookupDirectoryUsersAsync(
        UserDirectoryLookupQuery query,
        CancellationToken cancellationToken = default)
    {
        var search = query.Search.Trim();
        if (search.Length < 2)
        {
            return new UserDirectoryLookupResult(Array.Empty<UserDirectoryLookupItem>());
        }

        var maxResults = query.MaxResults switch
        {
            < 1 => 20,
            > 50 => 50,
            _ => query.MaxResults
        };

        var ldapSetting = await context.LdapSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted, cancellationToken);

        if (ldapSetting is null)
        {
            return new UserDirectoryLookupResult(Array.Empty<UserDirectoryLookupItem>());
        }

        var nationalIdAttrRaw = await context.ApplicationSettings
            .AsNoTracking()
            .Where(x =>
                x.Key == NationalIdApplicationSettingKey &&
                x.IsActive &&
                !x.IsDeleted)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);

        var nationalIdAttribute = string.IsNullOrWhiteSpace(nationalIdAttrRaw) ? null : nationalIdAttrRaw.Trim();

        string bindPassword;
        try
        {
            bindPassword = secretProtector.Unprotect(ldapSetting.EncryptedBindPassword);
        }
        catch
        {
            return new UserDirectoryLookupResult(Array.Empty<UserDirectoryLookupItem>());
        }

        if (string.IsNullOrWhiteSpace(bindPassword))
        {
            return new UserDirectoryLookupResult(Array.Empty<UserDirectoryLookupItem>());
        }

        var ldapResults = await ldapService.SearchUsersAsync(
            new LdapUserLookupRequest(
                ldapSetting.Host,
                ldapSetting.Port,
                ldapSetting.UseSsl,
                ldapSetting.BaseDn,
                ldapSetting.UserSearchBase,
                ldapSetting.BindUserName,
                ldapSetting.BindUserDomain,
                bindPassword,
                search,
                maxResults,
                nationalIdAttribute),
            cancellationToken);

        var ldapList = ldapResults.ToList();
        if (ldapList.Count == 0)
        {
            return new UserDirectoryLookupResult(Array.Empty<UserDirectoryLookupItem>());
        }

        var directoryIds = ldapList.Select(x => x.DirectoryObjectId).ToList();
        var directoryIdsLower = directoryIds
            .Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToList();

        var existingDirectoryIds = await context.PortalUsers
            .AsNoTracking()
            .Where(u => !u.IsDeleted && directoryIdsLower.Contains(u.DirectoryObjectId.ToLower()))
            .Select(u => u.DirectoryObjectId)
            .ToListAsync(cancellationToken);

        var existingSet = existingDirectoryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mapped = ldapList
            .Select(it => new UserDirectoryLookupItem(
                it.DirectoryObjectId,
                it.UserName,
                it.DisplayName,
                it.Email,
                MaskNationalId(it.NationalId),
                existingSet.Contains(it.DirectoryObjectId)))
            .OrderBy(x => x.UserName, StringComparer.Ordinal)
            .ToList();

        return new UserDirectoryLookupResult(mapped);
    }

    public async Task<CreateUserResult> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.DirectoryObjectId))
            {
                return new CreateUserResult(false, "Directory object id is required.", null);
            }

            var directoryObjectIdTrimmed = request.DirectoryObjectId.Trim();
            if (!Guid.TryParse(directoryObjectIdTrimmed, out _))
            {
                return new CreateUserResult(false, "Directory object id is invalid.", null);
            }

            var ldapSetting = await context.LdapSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted, cancellationToken);

            if (ldapSetting is null)
            {
                return new CreateUserResult(false, "LDAP settings are not configured.", null);
            }

            var nationalIdAttrRaw = await context.ApplicationSettings
                .AsNoTracking()
                .Where(x =>
                    x.Key == NationalIdApplicationSettingKey &&
                    x.IsActive &&
                    !x.IsDeleted)
                .Select(x => x.Value)
                .FirstOrDefaultAsync(cancellationToken);

            var nationalIdAttribute = string.IsNullOrWhiteSpace(nationalIdAttrRaw) ? null : nationalIdAttrRaw.Trim();

            string bindPassword;
            try
            {
                bindPassword = secretProtector.Unprotect(ldapSetting.EncryptedBindPassword);
            }
            catch
            {
                return new CreateUserResult(false, "LDAP settings are not configured.", null);
            }

            if (string.IsNullOrWhiteSpace(bindPassword))
            {
                return new CreateUserResult(false, "LDAP settings are not configured.", null);
            }

            var ldapProfile = await ldapService.GetUserProfileByObjectIdAsync(
                new LdapUserProfileByObjectIdRequest(
                    ldapSetting.Host,
                    ldapSetting.Port,
                    ldapSetting.UseSsl,
                    ldapSetting.BaseDn,
                    ldapSetting.UserSearchBase,
                    ldapSetting.BindUserName,
                    ldapSetting.BindUserDomain,
                    bindPassword,
                    directoryObjectIdTrimmed,
                    nationalIdAttribute),
                cancellationToken);

            if (ldapProfile is null)
            {
                return new CreateUserResult(false, "Directory user could not be found.", null);
            }

            var sameObjectExists = await context.PortalUsers.AnyAsync(
                u => !u.IsDeleted &&
                     u.DirectoryObjectId.ToUpper() == ldapProfile.DirectoryObjectId.ToUpperInvariant(),
                cancellationToken);
            if (sameObjectExists)
            {
                return new CreateUserResult(false, "Portal user already exists.", null);
            }

            var conflictingUserByName = await context.PortalUsers
                .FirstOrDefaultAsync(
                    u => !u.IsDeleted &&
                         u.UserName.ToUpper() == ldapProfile.UserName.ToUpperInvariant(),
                    cancellationToken);
            if (conflictingUserByName is not null &&
                !string.Equals(
                    conflictingUserByName.DirectoryObjectId,
                    ldapProfile.DirectoryObjectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new CreateUserResult(
                    false,
                    "A portal user with the same user name already exists.",
                    null);
            }

            var now = DateTime.UtcNow;

            string? encryptedNationalId =
                ldapProfile.NationalId is not null ? secretProtector.Protect(ldapProfile.NationalId) : null;
            var maskedNationalId = ldapProfile.NationalId is not null ? MaskNationalId(ldapProfile.NationalId) : null;

            var user = new PortalUser
            {
                DirectorySource = "ActiveDirectory",
                DirectoryObjectId = ldapProfile.DirectoryObjectId,
                NationalIdEncrypted = encryptedNationalId,
                NationalIdMasked = maskedNationalId,
                UserName = ldapProfile.UserName,
                DisplayName = ldapProfile.DisplayName,
                Email = ldapProfile.Email,
                IsActive = request.IsActive,
                CreatedAt = now,
                CreatedBy = request.ActorUserName ?? "system"
            };

            await context.PortalUsers.AddAsync(user, cancellationToken);

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = AuditActionType.Create,
                    EntityName = "PortalUser",
                    EntityId = user.Id.ToString(),
                    UserName = request.ActorUserName,
                    NewValues = """{"summary":"Portal user created."}""",
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new CreateUserResult(true, string.Empty, MapToDetail(user));
        }
        catch
        {
            return new CreateUserResult(false, "User could not be created.", null);
        }
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

    private static string? MaskNationalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var v = value.Trim();
        if (v.Length <= 4)
        {
            return new string('*', v.Length);
        }

        var prefix = v[..3];
        var suffix = v[^2..];
        var middleLen = v.Length - 5;
        var middle = middleLen > 0 ? new string('*', middleLen) : string.Empty;
        return prefix + middle + suffix;
    }
}
