using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Application.Common.Security;
using SasPortal.Domain.Entities;
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

        var normalizedUserName = request.UserName.Trim();

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
                    TestUserName = normalizedUserName,
                    TestPassword = request.Password
                },
                cancellationToken);

            if (!ldapResult.IsValid)
            {
                var description = BuildLoginFailedDescription(normalizedUserName, ldapResult.Message);
                var severity = GetLoginFailedSeverity(ldapResult.Message);

                await TryWriteSecurityLogAsync(
                    new SecurityLog
                    {
                        UserName = normalizedUserName,
                        EventType = "LoginFailed",
                        Severity = severity,
                        Description = description,
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);
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
                    UserName: normalizedUserName,
                    NationalIdAttribute: nationalIdAttribute),
                cancellationToken);

            if (ldapProfile is null)
            {
                await context.SecurityLogs.AddAsync(
                    new SecurityLog
                    {
                        UserName = normalizedUserName,
                        EventType = "LoginFailed",
                        Severity = "Warning",
                        Description = $"Login failed for {normalizedUserName}. Reason: directory user profile could not be loaded.",
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
                .AsSplitQuery()
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
                        UserName = normalizedUserName,
                        EventType = "LoginFailed",
                        Severity = "Warning",
                        Description = $"Login failed for {normalizedUserName}. Reason: user is not authorized to access the portal.",
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
                        UserId = user.Id,
                        UserName = user.UserName,
                        EventType = "LoginFailedUserPassive",
                        Severity = "Warning",
                        Description = $"Login failed for {user.UserName}. Reason: user is inactive.",
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
                        UserId = user.Id,
                        UserName = ldapProfile.UserName,
                        EventType = "LoginFailed",
                        Severity = "Warning",
                        Description = $"Login failed for {ldapProfile.UserName}. Reason: another portal user already uses this user name.",
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
                    UserId = user.Id,
                    UserName = user.UserName,
                    EventType = "LoginSuccess",
                    Severity = "Info",
                    Description = "Login succeeded.",
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
            await TryWriteSecurityLogAsync(
                new SecurityLog
                {
                    UserName = normalizedUserName,
                    EventType = "LoginError",
                    Severity = "Error",
                    Description = "Login could not be completed because an unexpected error occurred.",
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken);

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

    private static string BuildLoginFailedDescription(string userName, string? reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "directory authentication failed"
            : reason.Trim().TrimEnd('.').TrimEnd();

        normalizedReason = normalizedReason.Length == 0
            ? "directory authentication failed"
            : LowercaseFirstWordUnlessAcronym(normalizedReason);

        return $"Login failed for {userName}. Reason: {normalizedReason}.";
    }

    private static string LowercaseFirstWordUnlessAcronym(string value)
    {
        var firstWordEndIndex = value.IndexOf(' ');
        if (firstWordEndIndex < 0)
        {
            return IsAllUpperCase(value)
                ? value
                : char.ToLowerInvariant(value[0]) + value[1..];
        }

        var firstWord = value[..firstWordEndIndex];
        if (IsAllUpperCase(firstWord))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static bool IsAllUpperCase(string value)
    {
        var hasLetter = false;

        foreach (var ch in value)
        {
            if (!char.IsLetter(ch))
            {
                continue;
            }

            hasLetter = true;
            if (!char.IsUpper(ch))
            {
                return false;
            }
        }

        return hasLetter;
    }

    private static string GetLoginFailedSeverity(string? ldapMessage)
    {
        return ldapMessage?.Trim() switch
        {
            "Directory user could not be found." => "Warning",
            "Directory user authentication failed." => "Warning",
            "Directory user distinguished name could not be resolved." => "Warning",
            "LDAP service account authentication failed." => "Error",
            "Required LDAP fields are missing." => "Error",
            "LDAP validation failed." => "Error",
            _ => "Warning"
        };
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
                .AsSplitQuery()
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
                        EventType = "RefreshTokenFailed",
                        Severity = "Warning",
                        Description = "Invalid refresh token.",
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
                        UserId = refreshToken.PortalUserId,
                        UserName = refreshToken.PortalUser?.UserName,
                        EventType = "RefreshTokenFailed",
                        Severity = "Warning",
                        Description = "Invalid refresh token.",
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
                        UserId = refreshToken.PortalUserId,
                        UserName = refreshToken.PortalUser?.UserName,
                        EventType = "RefreshTokenFailed",
                        Severity = "Warning",
                        Description = "Refresh token has expired.",
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
                        EventType = "RefreshTokenFailed",
                        Severity = "Warning",
                        Description = "Invalid refresh token.",
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
                        UserId = user.Id,
                        UserName = user.UserName,
                        EventType = "RefreshTokenFailed",
                        Severity = "Warning",
                        Description = "User is inactive.",
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
                    UserId = user.Id,
                    UserName = user.UserName,
                    EventType = "RefreshTokenCreated",
                    Severity = "Info",
                    Description = "Refresh token issued.",
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
            await TryWriteSecurityLogAsync(
                new SecurityLog
                {
                    EventType = "RefreshTokenError",
                    Severity = "Error",
                    Description = "Token refresh could not be completed because an unexpected error occurred.",
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken);

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
                        EventType = "Logout",
                        Severity = "Warning",
                        Description = "Logout failed. Refresh token was not found.",
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
                        UserId = refreshToken.PortalUserId,
                        UserName = refreshToken.PortalUser?.UserName,
                        EventType = "Logout",
                        Severity = "Warning",
                        Description = "Logout failed. Refresh token was already revoked.",
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
                        UserId = refreshToken.PortalUserId,
                        UserName = refreshToken.PortalUser?.UserName,
                        EventType = "Logout",
                        Severity = "Warning",
                        Description = "Logout failed. Refresh token has expired.",
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
                    UserId = refreshToken.PortalUserId,
                    UserName = refreshToken.PortalUser?.UserName,
                    EventType = "Logout",
                    Severity = "Info",
                    Description = "Logout succeeded.",
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
            await TryWriteSecurityLogAsync(
                new SecurityLog
                {
                    EventType = "LogoutError",
                    Severity = "Error",
                    Description = "Logout could not be completed because an unexpected error occurred.",
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken);

            return new LogoutResult(false, "Logout could not be completed.");
        }
    }

    private async Task TryWriteSecurityLogAsync(SecurityLog securityLog, CancellationToken cancellationToken)
    {
        try
        {
            await context.SecurityLogs.AddAsync(securityLog, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            context.Entry(securityLog).State = EntityState.Detached;
        }
    }

    public async Task<CurrentUserResult> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await context.PortalUsers
                .AsNoTracking()
                .AsSplitQuery()
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

            return MapToCurrentUserResult(user, roles, permissions, isSuperAdmin);
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
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false);
        }
    }

    public async Task<UpdateCurrentUserPreferencesResult> UpdateCurrentUserPreferencesAsync(
        UpdateCurrentUserPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PreferredLanguage))
            {
                return new UpdateCurrentUserPreferencesResult(
                    false,
                    "Preferred language is not supported.",
                    null);
            }

            var normalizedLanguage = SupportedLanguages.Normalize(request.PreferredLanguage);
            if (!SupportedLanguages.IsSupported(normalizedLanguage))
            {
                return new UpdateCurrentUserPreferencesResult(
                    false,
                    "Preferred language is not supported.",
                    null);
            }

            var user = await context.PortalUsers
                .AsSplitQuery()
                .Include(x => x.UserRoles)
                    .ThenInclude(x => x.PortalRole)
                        .ThenInclude(x => x.RolePermissions)
                            .ThenInclude(x => x.PortalPermission)
                .FirstOrDefaultAsync(
                    x => !x.IsDeleted && x.Id == request.UserId,
                    cancellationToken);

            if (user is null)
            {
                return new UpdateCurrentUserPreferencesResult(false, "User was not found.", null);
            }

            if (!user.IsActive)
            {
                return new UpdateCurrentUserPreferencesResult(false, "User is inactive.", null);
            }

            var now = DateTime.UtcNow;
            user.PreferredLanguage = normalizedLanguage;
            user.UpdatedAt = now;
            user.UpdatedBy = request.ActorUserName ?? "auth";

            await context.SaveChangesAsync(cancellationToken);

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

            var isSuperAdmin = roles.Contains(SuperAdminRoleCode, StringComparer.OrdinalIgnoreCase);
            var currentUser = MapToCurrentUserResult(user, roles, permissions, isSuperAdmin);

            return new UpdateCurrentUserPreferencesResult(true, "User preferences updated.", currentUser);
        }
        catch
        {
            return new UpdateCurrentUserPreferencesResult(
                false,
                "User preferences could not be updated.",
                null);
        }
    }

    private static CurrentUserResult MapToCurrentUserResult(
        PortalUser user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        bool isSuperAdmin)
    {
        var preferredLanguage = string.IsNullOrWhiteSpace(user.PreferredLanguage)
            ? SupportedLanguages.Turkish
            : SupportedLanguages.Normalize(user.PreferredLanguage);

        return new CurrentUserResult(
            true,
            "Succeeded.",
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            preferredLanguage,
            roles,
            permissions,
            isSuperAdmin);
    }
}
