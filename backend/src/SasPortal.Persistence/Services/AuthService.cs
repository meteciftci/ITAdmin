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

            var user = await context.PortalUsers
                .Include(x => x.UserRoles)
                    .ThenInclude(x => x.PortalRole)
                        .ThenInclude(x => x.RolePermissions)
                            .ThenInclude(x => x.PortalPermission)
                .FirstOrDefaultAsync(x => x.UserName == request.UserName && !x.IsDeleted, cancellationToken);

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
}
