using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class AuthService(
    AppDbContext context,
    ILdapService ldapService,
    ISecretProtector secretProtector,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private const string SuperAdminRoleCode = "SuperAdmin";
    private const string ActiveDirectoryDirectorySource = "ActiveDirectory";
    private const string NationalIdApplicationSettingKey = "Directory:NationalIdAttribute";

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<AuthTokenResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return new AuthTokenResult(false, "User name is required.", null, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthTokenResult(false, "Password is required.", null, null, null, null);
        }

        try
        {
            var ldapSetting = await context.LdapSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted, cancellationToken);

            if (ldapSetting is null)
            {
                return new AuthTokenResult(false, "LDAP settings are not configured.", null, null, null, null);
            }

            var bindPassword = secretProtector.Unprotect(ldapSetting.EncryptedBindPassword);

            var ldapResult = await ldapService.ValidateAsync(
                new LdapValidationRequest
                {
                    Host = ldapSetting.Host,
                    Port = ldapSetting.Port,
                    UseSsl = ldapSetting.UseSsl,
                    BaseDn = ldapSetting.BaseDn,
                    UserSearchBase = ldapSetting.UserSearchBase,
                    UserSearchFilter = ldapSetting.UserSearchFilter,
                    BindUserName = ldapSetting.BindUserName,
                    BindUserDomain = ldapSetting.BindUserDomain,
                    BindPassword = bindPassword,
                    TestUserName = request.UserName,
                    TestPassword = request.Password
                },
                cancellationToken);

            if (!ldapResult.IsValid)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        UserName = request.UserName,
                        EventType = SecurityEventType.LoginFailed,
                        IsSuccess = false,
                        Message = ldapResult.Message,
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(false, ldapResult.Message, null, null, null, null);
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

            var ldapProfile = await ldapService.GetUserProfileAsync(
                new LdapUserProfileRequest(
                    Host: ldapSetting.Host,
                    Port: ldapSetting.Port,
                    UseSsl: ldapSetting.UseSsl,
                    BaseDn: ldapSetting.BaseDn,
                    UserSearchBase: ldapSetting.UserSearchBase,
                    UserSearchFilter: ldapSetting.UserSearchFilter,
                    BindUserName: ldapSetting.BindUserName,
                    BindUserDomain: ldapSetting.BindUserDomain,
                    BindPassword: bindPassword,
                    UserName: request.UserName,
                    NationalIdAttribute: nationalIdAttribute),
                cancellationToken);

            if (ldapProfile is null)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        UserName = request.UserName,
                        EventType = SecurityEventType.LoginFailed,
                        IsSuccess = false,
                        Message = "Directory user profile could not be loaded.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);

                return new AuthTokenResult(
                    false,
                    "Directory user profile could not be loaded.",
                    null,
                    null,
                    null,
                    null);
            }

            var user = await context.PortalUsers
                .Include(x => x.UserRoles)
                    .ThenInclude(x => x.PortalRole)
                        .ThenInclude(x => x.RolePermissions)
                            .ThenInclude(x => x.PortalPermission)
                .FirstOrDefaultAsync(x => x.DirectoryObjectId == ldapProfile.DirectoryObjectId, cancellationToken);

            if (user is null)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        UserName = request.UserName,
                        EventType = SecurityEventType.LoginFailed,
                        IsSuccess = false,
                        Message = "User is not authorized to access the portal.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(false, "User is not authorized to access the portal.", null, null, null, null);
            }

            if (!user.IsActive)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        PortalUserId = user.Id,
                        UserName = user.UserName,
                        EventType = SecurityEventType.LoginFailed,
                        IsSuccess = false,
                        Message = "User is inactive.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(false, "User is inactive.", null, null, null, null);
            }

            var userNameConflict = await context.PortalUsers.AnyAsync(
                x =>
                    x.UserName == ldapProfile.UserName &&
                    x.Id != user.Id,
                cancellationToken);

            if (userNameConflict)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        PortalUserId = user.Id,
                        UserName = ldapProfile.UserName,
                        EventType = SecurityEventType.LoginFailed,
                        IsSuccess = false,
                        Message = "Another portal user already uses this user name.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(
                    false,
                    "Another portal user already uses this user name.",
                    null,
                    null,
                    null,
                    null);
            }

            user.UserName = ldapProfile.UserName;
            user.DisplayName = ldapProfile.DisplayName;
            user.Email = ldapProfile.Email;
            user.DirectorySource = ActiveDirectoryDirectorySource;
            user.DirectoryObjectId = ldapProfile.DirectoryObjectId;

            if (ldapProfile.NationalId is not null)
            {
                user.NationalIdEncrypted = secretProtector.Protect(ldapProfile.NationalId);
                user.NationalIdMasked = MaskNationalId(ldapProfile.NationalId);
            }

            var activeRoles = user.UserRoles
                .Where(x => x.PortalRole.IsActive && !x.PortalRole.IsDeleted)
                .Select(x => x.PortalRole)
                .ToList();

            var roles = activeRoles
                .Select(x => x.Code)
                .ToList();

            var permissions = activeRoles
                .SelectMany(x => x.RolePermissions)
                .Where(x => x.PortalPermission.IsActive && !x.PortalPermission.IsDeleted)
                .Select(x => x.PortalPermission.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var userInfo = new AuthenticatedUserInfo(
                user.Id,
                user.UserName,
                user.DisplayName,
                user.Email,
                roles,
                permissions);

            var now = DateTime.UtcNow;
            var accessExpiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
            var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

            var accessToken = tokenService.CreateAccessToken(userInfo, accessExpiresAt);
            var refreshToken = tokenService.CreateRefreshToken();
            var refreshTokenHash = tokenService.HashRefreshToken(refreshToken);

            await context.RefreshTokens.AddAsync(
                new RefreshToken
                {
                    PortalUserId = user.Id,
                    TokenHash = refreshTokenHash,
                    ExpiresAt = refreshExpiresAt,
                    CreatedAt = now,
                    CreatedByIp = request.IpAddress,
                    UserAgent = request.UserAgent
                },
                cancellationToken);

            user.LastLoginAt = now;
            user.UpdatedAt = now;
            user.UpdatedBy = "auth";

            await context.SecurityLogs.AddAsync(
                new SecurityLog
                {
                    PortalUserId = user.Id,
                    UserName = user.UserName,
                    EventType = SecurityEventType.LoginSucceeded,
                    IsSuccess = true,
                    Message = "Login succeeded.",
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new AuthTokenResult(
                true,
                "Login succeeded.",
                accessToken,
                refreshToken,
                accessExpiresAt,
                refreshExpiresAt);
        }
        catch
        {
            return new AuthTokenResult(false, "Login could not be completed.", null, null, null, null);
        }
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

    public async Task<AuthTokenResult> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new AuthTokenResult(false, "Refresh token is required.", null, null, null, null);
        }

        try
        {
            var now = DateTime.UtcNow;
            var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);

            var refreshToken = await context.RefreshTokens
                .Include(x => x.PortalUser)
                    .ThenInclude(x => x.UserRoles)
                        .ThenInclude(x => x.PortalRole)
                            .ThenInclude(x => x.RolePermissions)
                                .ThenInclude(x => x.PortalPermission)
                .FirstOrDefaultAsync(x => x.TokenHash == refreshTokenHash, cancellationToken);

            if (refreshToken is null)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        EventType = SecurityEventType.RefreshTokenRevoked,
                        IsSuccess = false,
                        Message = "Invalid refresh token.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = now
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(false, "Invalid refresh token.", null, null, null, null);
            }

            if (refreshToken.RevokedAt is not null)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        PortalUserId = refreshToken.PortalUserId,
                        UserName = refreshToken.PortalUser?.UserName,
                        EventType = SecurityEventType.RefreshTokenRevoked,
                        IsSuccess = false,
                        Message = "Invalid refresh token.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = now
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(false, "Invalid refresh token.", null, null, null, null);
            }

            if (refreshToken.ExpiresAt <= now)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        PortalUserId = refreshToken.PortalUserId,
                        UserName = refreshToken.PortalUser?.UserName,
                        EventType = SecurityEventType.RefreshTokenRevoked,
                        IsSuccess = false,
                        Message = "Refresh token has expired.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = now
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(false, "Refresh token has expired.", null, null, null, null);
            }

            var user = refreshToken.PortalUser;
            if (user is null || user.IsDeleted)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        EventType = SecurityEventType.RefreshTokenRevoked,
                        IsSuccess = false,
                        Message = "Invalid refresh token.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = now
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(false, "Invalid refresh token.", null, null, null, null);
            }

            if (!user.IsActive)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        PortalUserId = user.Id,
                        UserName = user.UserName,
                        EventType = SecurityEventType.RefreshTokenRevoked,
                        IsSuccess = false,
                        Message = "User is inactive.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = now
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new AuthTokenResult(false, "User is inactive.", null, null, null, null);
            }

            var activeRoles = user.UserRoles
                .Where(x => x.PortalRole.IsActive && !x.PortalRole.IsDeleted)
                .Select(x => x.PortalRole)
                .ToList();

            var roles = activeRoles
                .Select(x => x.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var permissions = activeRoles
                .SelectMany(x => x.RolePermissions)
                .Where(x => x.PortalPermission.IsActive && !x.PortalPermission.IsDeleted)
                .Select(x => x.PortalPermission.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var userInfo = new AuthenticatedUserInfo(
                user.Id,
                user.UserName,
                user.DisplayName,
                user.Email,
                roles,
                permissions);

            var accessExpiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
            var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

            var newAccessToken = tokenService.CreateAccessToken(userInfo, accessExpiresAt);
            var newRefreshToken = tokenService.CreateRefreshToken();
            var newRefreshTokenHash = tokenService.HashRefreshToken(newRefreshToken);

            refreshToken.RevokedAt = now;
            refreshToken.RevokedByIp = request.IpAddress;
            refreshToken.ReplacedByTokenHash = newRefreshTokenHash;

            await context.RefreshTokens.AddAsync(
                new RefreshToken
                {
                    PortalUserId = user.Id,
                    TokenHash = newRefreshTokenHash,
                    ExpiresAt = refreshExpiresAt,
                    CreatedAt = now,
                    CreatedByIp = request.IpAddress,
                    UserAgent = request.UserAgent
                },
                cancellationToken);

            await context.SecurityLogs.AddAsync(
                new SecurityLog
                {
                    PortalUserId = user.Id,
                    UserName = user.UserName,
                    EventType = SecurityEventType.RefreshTokenIssued,
                    IsSuccess = true,
                    Message = "Refresh token issued.",
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return new AuthTokenResult(
                true,
                "Token refreshed.",
                newAccessToken,
                newRefreshToken,
                accessExpiresAt,
                refreshExpiresAt);
        }
        catch
        {
            return new AuthTokenResult(false, "Token refresh could not be completed.", null, null, null, null);
        }
    }

    public async Task<LogoutResult> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new LogoutResult(false, "Refresh token is required.");
        }

        try
        {
            var now = DateTime.UtcNow;
            var refreshTokenHash = tokenService.HashRefreshToken(request.RefreshToken);

            var refreshToken = await context.RefreshTokens
                .Include(x => x.PortalUser)
                .FirstOrDefaultAsync(x => x.TokenHash == refreshTokenHash, cancellationToken);

            if (refreshToken is null)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        EventType = SecurityEventType.Logout,
                        IsSuccess = false,
                        Message = "Logout failed. Refresh token was not found.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = now
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new LogoutResult(false, "Invalid refresh token.");
            }

            if (refreshToken.RevokedAt is not null)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        PortalUserId = refreshToken.PortalUserId,
                        UserName = refreshToken.PortalUser?.UserName,
                        EventType = SecurityEventType.Logout,
                        IsSuccess = false,
                        Message = "Logout failed. Refresh token was already revoked.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = now
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new LogoutResult(false, "Refresh token is already revoked.");
            }

            if (refreshToken.ExpiresAt <= now)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        PortalUserId = refreshToken.PortalUserId,
                        UserName = refreshToken.PortalUser?.UserName,
                        EventType = SecurityEventType.Logout,
                        IsSuccess = false,
                        Message = "Logout failed. Refresh token has expired.",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = now
                    },
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                return new LogoutResult(false, "Refresh token has expired.");
            }

            refreshToken.RevokedAt = now;
            refreshToken.RevokedByIp = request.IpAddress;

            await context.SecurityLogs.AddAsync(
                new SecurityLog
                {
                    PortalUserId = refreshToken.PortalUserId,
                    UserName = refreshToken.PortalUser?.UserName,
                    EventType = SecurityEventType.Logout,
                    IsSuccess = true,
                    Message = "Logout succeeded.",
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            return new LogoutResult(true, "Logout succeeded.");
        }
        catch
        {
            return new LogoutResult(false, "Logout could not be completed.");
        }
    }

    public async Task<CurrentUserResult> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await context.PortalUsers
                .AsNoTracking()
                .Include(x => x.UserRoles)
                    .ThenInclude(x => x.PortalRole)
                        .ThenInclude(x => x.RolePermissions)
                            .ThenInclude(x => x.PortalPermission)
                .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

            if (user is null)
            {
                return new CurrentUserResult(
                    false,
                    "User was not found.",
                    null,
                    null,
                    null,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    false);
            }

            if (user.IsDeleted)
            {
                return new CurrentUserResult(
                    false,
                    "User was not found.",
                    null,
                    null,
                    null,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    false);
            }

            if (!user.IsActive)
            {
                return new CurrentUserResult(
                    false,
                    "User is inactive.",
                    null,
                    null,
                    null,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    false);
            }

            var activeRoles = user.UserRoles
                .Where(x => x.PortalRole.IsActive && !x.PortalRole.IsDeleted)
                .Select(x => x.PortalRole)
                .ToList();

            var roles = activeRoles
                .Select(x => x.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var isSuperAdmin = roles.Contains(SuperAdminRoleCode, StringComparer.OrdinalIgnoreCase);

            var permissions = activeRoles
                .SelectMany(x => x.RolePermissions)
                .Where(x => x.PortalPermission.IsActive && !x.PortalPermission.IsDeleted)
                .Select(x => x.PortalPermission.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new CurrentUserResult(
                true,
                "Succeeded.",
                user.Id,
                user.UserName,
                user.DisplayName,
                user.Email,
                roles,
                permissions,
                isSuperAdmin);
        }
        catch
        {
            return new CurrentUserResult(
                false,
                "Current user could not be retrieved.",
                null,
                null,
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false);
        }
    }
}
