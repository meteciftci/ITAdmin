using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Application.Common.Security;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class UserService(
    AppDbContext context,
    ILdapService ldapService,
    ISecretProtector secretProtector) : IUserService
{
    private const string NationalIdApplicationSettingKey = "Directory:NationalIdAttribute";
    private const int AuditDescriptionMaxLength = 2000;

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
                PreferredLanguage = "tr",
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
                    Action = "Create",
                    EntityName = "PortalUser",
                    EntityId = user.Id.ToString(),
                    Description = BuildCreateUserAuditDescription(user),
                    ActorUserName = request.ActorUserName,
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
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

    public async Task<UpdateUserStatusResult> UpdateUserStatusAsync(
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await context.PortalUsers
                .Where(x => !x.IsDeleted && x.Id == request.UserId)
                .Include(x => x.UserRoles)
                    .ThenInclude(ur => ur.PortalRole)
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return new UpdateUserStatusResult(false, "User was not found.", null);
            }

            if (!request.IsActive &&
                request.ActorUserId.HasValue &&
                request.ActorUserId.Value == request.UserId)
            {
                return new UpdateUserStatusResult(
                    false,
                    "You cannot deactivate your own user account.",
                    null);
            }

            if (!request.IsActive && UserHasActiveSuperAdminRole(user))
            {
                var hasAnotherActiveSuperAdmin =
                    await HasAnotherActiveSuperAdminExcludingAsync(request.UserId, cancellationToken);

                if (!hasAnotherActiveSuperAdmin)
                {
                    return new UpdateUserStatusResult(
                        false,
                        "The last active SuperAdmin user cannot be deactivated.",
                        null);
                }
            }

            if (user.IsActive == request.IsActive)
            {
                return new UpdateUserStatusResult(true, string.Empty, MapToDetail(user));
            }

            var now = DateTime.UtcNow;
            var oldStatus = user.IsActive;

            user.IsActive = request.IsActive;
            user.UpdatedAt = now;
            user.UpdatedBy = request.ActorUserName ?? "system";

            var actionSummary = request.IsActive
                ? "Portal user activated"
                : "Portal user deactivated";
            var auditSummary =
                $"{actionSummary}: {FormatUserIdentity(user)}. Status: {FormatStatus(oldStatus)} -> {FormatStatus(request.IsActive)}.";

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Update",
                    EntityName = "PortalUser",
                    EntityId = user.Id.ToString(),
                    Description = TruncateAuditDescription(auditSummary),
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new UpdateUserStatusResult(true, string.Empty, MapToDetail(user));
        }
        catch
        {
            return new UpdateUserStatusResult(false, "User status could not be updated.", null);
        }
    }

    public async Task<UpdateUserRolesResult> UpdateUserRolesAsync(
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.RoleIds is null)
            {
                return new UpdateUserRolesResult(false, "Role ids are required.", null);
            }

            var roleIds = request.RoleIds.Distinct().ToList();
            var user = await context.PortalUsers
                .Where(x => !x.IsDeleted && x.Id == request.UserId)
                .Include(x => x.UserRoles)
                    .ThenInclude(ur => ur.PortalRole)
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return new UpdateUserRolesResult(false, "User was not found.", null);
            }

            List<PortalRole> requestedRoles = [];
            if (roleIds.Count > 0)
            {
                requestedRoles = await context.PortalRoles
                    .Where(x => !x.IsDeleted && roleIds.Contains(x.Id))
                    .ToListAsync(cancellationToken);

                if (requestedRoles.Count != roleIds.Count)
                {
                    return new UpdateUserRolesResult(false, "One or more roles were not found.", null);
                }

                if (requestedRoles.Any(x => !x.IsActive))
                {
                    return new UpdateUserRolesResult(false, "One or more roles are inactive.", null);
                }
            }

            var currentlyHasSuperAdmin = UserHasActiveSuperAdminRole(user);
            var willHaveSuperAdmin = requestedRoles.Any(x =>
                string.Equals(x.Code, SystemRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));

            if (request.ActorUserId.HasValue &&
                request.ActorUserId.Value == user.Id &&
                currentlyHasSuperAdmin &&
                !willHaveSuperAdmin)
            {
                return new UpdateUserRolesResult(false, "You cannot remove your own SuperAdmin role.", null);
            }

            if (user.IsActive &&
                currentlyHasSuperAdmin &&
                !willHaveSuperAdmin)
            {
                var hasAnotherActiveSuperAdmin =
                    await HasAnotherActiveSuperAdminExcludingAsync(user.Id, cancellationToken);

                if (!hasAnotherActiveSuperAdmin)
                {
                    return new UpdateUserRolesResult(
                        false,
                        "The last active SuperAdmin user cannot lose the SuperAdmin role.",
                        null);
                }
            }

            var now = DateTime.UtcNow;
            var actor = request.ActorUserName ?? "system";
            var currentRoleCodes = user.UserRoles
                .Where(ur => ur.PortalRole.IsActive && !ur.PortalRole.IsDeleted)
                .Select(ur => ur.PortalRole.Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            var requestedRoleCodes = requestedRoles
                .Select(role => role.Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            var addedRoleCodes = requestedRoleCodes
                .Except(currentRoleCodes, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            var removedRoleCodes = currentRoleCodes
                .Except(requestedRoleCodes, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            var requestedRoleIdSet = roleIds.ToHashSet();
            var currentRoleIdSet = user.UserRoles
                .Select(ur => ur.PortalRoleId)
                .ToHashSet();

            var userRolesToRemove = user.UserRoles
                .Where(ur => !requestedRoleIdSet.Contains(ur.PortalRoleId))
                .ToList();

            if (userRolesToRemove.Count > 0)
            {
                context.PortalUserRoles.RemoveRange(userRolesToRemove);
                foreach (var userRole in userRolesToRemove)
                {
                    user.UserRoles.Remove(userRole);
                }
            }

            var userRolesToAdd = roleIds
                .Where(roleId => !currentRoleIdSet.Contains(roleId))
                .Select(roleId => new PortalUserRole
                {
                    PortalUserId = user.Id,
                    PortalRoleId = roleId,
                    CreatedAt = now,
                    CreatedBy = actor
                })
                .ToList();

            if (userRolesToAdd.Count > 0)
            {
                await context.PortalUserRoles.AddRangeAsync(userRolesToAdd, cancellationToken);
            }

            user.UpdatedAt = now;
            user.UpdatedBy = actor;

            await context.AuditLogs.AddAsync(
                new AuditLog
                {
                    Action = "Update",
                    EntityName = "PortalUser",
                    EntityId = user.Id.ToString(),
                    Description = BuildUpdateUserRolesAuditDescription(user, addedRoleCodes, removedRoleCodes),
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            await context.Entry(user)
                .Collection(x => x.UserRoles)
                .Query()
                .Include(x => x.PortalRole)
                .LoadAsync(cancellationToken);

            return new UpdateUserRolesResult(true, string.Empty, MapToDetail(user));
        }
        catch
        {
            return new UpdateUserRolesResult(false, "User roles could not be updated.", null);
        }
    }

    private async Task<bool> HasAnotherActiveSuperAdminExcludingAsync(
        Guid excludedUserId,
        CancellationToken cancellationToken) =>
        await context.PortalUsers
            .AsNoTracking()
            .Where(u =>
                u.Id != excludedUserId &&
                !u.IsDeleted &&
                u.IsActive)
            .AnyAsync(u =>
                    u.UserRoles.Any(ur =>
                        ur.PortalRole.IsActive &&
                        !ur.PortalRole.IsDeleted &&
                        string.Equals(
                            ur.PortalRole.Code,
                            SystemRoles.SuperAdmin,
                            StringComparison.OrdinalIgnoreCase)),
                cancellationToken);

    private static bool UserHasActiveSuperAdminRole(PortalUser user) =>
        user.UserRoles.Any(ur =>
            ur.PortalRole.IsActive &&
            !ur.PortalRole.IsDeleted &&
            string.Equals(ur.PortalRole.Code, SystemRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));

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

    private static string BuildCreateUserAuditDescription(PortalUser user)
    {
        var baseText = $"Portal user created: {FormatUserIdentity(user)}";
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return TruncateAuditDescription($"{baseText}, email: {user.Email.Trim()}.");
        }

        return TruncateAuditDescription($"{baseText}.");
    }

    private static string BuildUpdateUserRolesAuditDescription(
        PortalUser user,
        IReadOnlyList<string> addedRoleCodes,
        IReadOnlyList<string> removedRoleCodes)
    {
        var summary = $"Portal user roles updated: {FormatUserIdentity(user)}.";
        var addedText = FormatChangedList("Added roles", addedRoleCodes);
        var removedText = FormatChangedList("Removed roles", removedRoleCodes);

        if (addedText is null && removedText is null)
        {
            return TruncateAuditDescription($"{summary} No role changes.");
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

    private static string FormatUserIdentity(PortalUser user) => $"{user.UserName} ({user.DisplayName})";

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
