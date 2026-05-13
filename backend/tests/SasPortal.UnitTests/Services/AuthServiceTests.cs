using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.Services;

public sealed class AuthServiceTests
{
    private const string IpAddress = "10.20.30.40";
    private const string UserAgent = "xunit-agent";

    [Fact]
    public async Task LoginAsync_WhenUserNameMissing_ReturnsFailure_AndDoesNotWriteSecurityLog()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("User name is required.", result.Message);
        Assert.Empty(await testContext.DbContext.SecurityLogs.ToListAsync());
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordMissing_ReturnsFailure_AndDoesNotWriteSecurityLog()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete", "", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Password is required.", result.Message);
        Assert.Empty(await testContext.DbContext.SecurityLogs.ToListAsync());
    }

    [Fact]
    public async Task LoginAsync_WhenLdapSettingsMissing_ReturnsFailure_AndDoesNotWriteSecurityLog()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("LDAP settings are not configured.", result.Message);
        Assert.Empty(await testContext.DbContext.SecurityLogs.ToListAsync());
    }

    [Fact]
    public async Task LoginAsync_WhenDirectoryUserAuthenticationFails_WritesLoginFailedWarning()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(false, "Directory user authentication failed.");

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("  mete.user  ", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Directory user authentication failed.", result.Message);

        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginFailed", log.EventType);
        Assert.Equal("Warning", log.Severity);
        Assert.Equal("mete.user", log.UserName);
        Assert.Equal("Login failed for mete.user. Reason: directory user authentication failed.", log.Description);
        Assert.Equal(IpAddress, log.IpAddress);
        Assert.Equal(UserAgent, log.UserAgent);
        Assert.DoesNotContain("password", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_WhenDirectoryUserNotFound_WritesLoginFailedWarning()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(false, "Directory user could not be found.");

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete.user", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginFailed", log.EventType);
        Assert.Equal("Warning", log.Severity);
        Assert.Equal("Login failed for mete.user. Reason: directory user could not be found.", log.Description);
    }

    [Fact]
    public async Task LoginAsync_WhenLdapServiceAccountAuthenticationFails_WritesLoginFailedError()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(false, "LDAP service account authentication failed.");

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete.user", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginFailed", log.EventType);
        Assert.Equal("Error", log.Severity);
        Assert.Equal("Login failed for mete.user. Reason: LDAP service account authentication failed.", log.Description);
    }

    [Fact]
    public async Task LoginAsync_WhenRequiredLdapFieldsMissing_WritesLoginFailedError()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(false, "Required LDAP fields are missing.");

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete.user", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginFailed", log.EventType);
        Assert.Equal("Error", log.Severity);
        Assert.Equal("Login failed for mete.user. Reason: required LDAP fields are missing.", log.Description);
    }

    [Fact]
    public async Task LoginAsync_WhenLdapValidationFailed_WritesLoginFailedError()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(false, "LDAP validation failed.");

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete.user", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginFailed", log.EventType);
        Assert.Equal("Error", log.Severity);
        Assert.Equal("Login failed for mete.user. Reason: LDAP validation failed.", log.Description);
    }

    [Fact]
    public async Task LoginAsync_WhenLdapProfileCannotBeLoaded_WritesLoginFailedWarning()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = null;

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete.user", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Directory user profile could not be loaded.", result.Message);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginFailed", log.EventType);
        Assert.Equal("Warning", log.Severity);
        Assert.Contains("directory user profile could not be loaded", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("mete.user", log.UserName);
    }

    [Fact]
    public async Task LoginAsync_WhenPortalUserDoesNotExist_WritesLoginFailedWarning()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile();

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete.user", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("User is not authorized to access the portal.", result.Message);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginFailed", log.EventType);
        Assert.Equal("Warning", log.Severity);
        Assert.Contains("user is not authorized to access the portal", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_WhenPortalUserInactive_WritesLoginFailedUserPassiveWarning()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        var user = await SeedPortalUserAsync(testContext.DbContext, isActive: false);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile(user.DirectoryObjectId, user.UserName);

        var result = await testContext.AuthService.LoginAsync(new LoginRequest(user.UserName, "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("User is inactive.", result.Message);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginFailedUserPassive", log.EventType);
        Assert.Equal("Warning", log.Severity);
        Assert.Equal(user.Id, log.UserId);
        Assert.Equal(user.UserName, log.UserName);
        Assert.Contains("user is inactive", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_WhenLoginSucceeds_WritesLoginSuccessInfoAndRefreshToken()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        var user = await SeedPortalUserWithRolePermissionAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile(user.DirectoryObjectId, user.UserName);

        var result = await testContext.AuthService.LoginAsync(new LoginRequest(user.UserName, "password", IpAddress, UserAgent));

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token-0", result.RefreshToken);

        var refreshToken = await testContext.DbContext.RefreshTokens.SingleAsync();
        Assert.Equal(user.Id, refreshToken.PortalUserId);
        Assert.Equal("hash:refresh-token-0", refreshToken.TokenHash);
        Assert.Equal(IpAddress, refreshToken.CreatedByIp);
        Assert.Equal(UserAgent, refreshToken.UserAgent);
        Assert.False(refreshToken.IsPersistent);
        var sessionHours = (refreshToken.ExpiresAt - DateTime.UtcNow).TotalHours;
        Assert.InRange(sessionHours, 5.9, 6.1);

        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginSuccess", log.EventType);
        Assert.Equal("Info", log.Severity);
        Assert.Equal(user.Id, log.UserId);
        Assert.Equal(user.UserName, log.UserName);
        Assert.Equal(IpAddress, log.IpAddress);
        Assert.Equal(UserAgent, log.UserAgent);
        Assert.DoesNotContain("password", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bind-secret", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh-token", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access-token", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_WhenRememberMeTrue_CreatesPersistentRefreshTokenWithRememberMeExpiry()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        var user = await SeedPortalUserWithRolePermissionAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile(user.DirectoryObjectId, user.UserName);

        var result = await testContext.AuthService.LoginAsync(
            new LoginRequest(user.UserName, "password", IpAddress, UserAgent, RememberMe: true));

        Assert.True(result.IsSuccess);
        var refreshToken = await testContext.DbContext.RefreshTokens.SingleAsync();
        Assert.True(refreshToken.IsPersistent);
        var rememberMeDays = (refreshToken.ExpiresAt - DateTime.UtcNow).TotalDays;
        Assert.InRange(rememberMeDays, 6.9, 7.1);
    }

    [Fact]
    public async Task LoginAsync_WhenRememberMeDisabled_IgnoresRememberMeTrue()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        testContext.SettingsService.SessionSecurity = new SessionSecuritySettings(30, 30, 30, 6, 7, false);
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        var user = await SeedPortalUserWithRolePermissionAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile(user.DirectoryObjectId, user.UserName);

        var result = await testContext.AuthService.LoginAsync(
            new LoginRequest(user.UserName, "password", IpAddress, UserAgent, RememberMe: true));

        Assert.True(result.IsSuccess);
        var refreshToken = await testContext.DbContext.RefreshTokens.SingleAsync();
        Assert.False(refreshToken.IsPersistent);
        var sessionHours = (refreshToken.ExpiresAt - DateTime.UtcNow).TotalHours;
        Assert.InRange(sessionHours, 5.9, 6.1);
    }

    [Fact]
    public async Task LoginAsync_UsesAccessTokenMinutesFromSessionSecuritySettings()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        testContext.SettingsService.SessionSecurity = new SessionSecuritySettings(45, 30, 30, 6, 7, true);
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        var user = await SeedPortalUserWithRolePermissionAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile(user.DirectoryObjectId, user.UserName);

        var result = await testContext.AuthService.LoginAsync(new LoginRequest(user.UserName, "password", IpAddress, UserAgent));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AccessTokenExpiresAt);
        var accessMinutes = (result.AccessTokenExpiresAt.Value - DateTime.UtcNow).TotalMinutes;
        Assert.InRange(accessMinutes, 43, 47);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenIdleExpired_RevokesTokenAndReturnsSessionIdleTimeout()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        testContext.SettingsService.SessionSecurity = new SessionSecuritySettings(30, 30, 30, 6, 7, true);
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        var user = await SeedPortalUserWithRolePermissionAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile(user.DirectoryObjectId, user.UserName);

        var loginResult = await testContext.AuthService.LoginAsync(
            new LoginRequest(user.UserName, "password", IpAddress, UserAgent));
        Assert.True(loginResult.IsSuccess);

        // Backdate LastUsedAt so the idle window has clearly elapsed while keeping the absolute
        // ExpiresAt in the future. Logout/refresh tests share this invariant.
        var issued = await testContext.DbContext.RefreshTokens.SingleAsync();
        issued.LastUsedAt = DateTime.UtcNow.AddMinutes(-31);
        issued.ExpiresAt = DateTime.UtcNow.AddHours(5);
        await testContext.DbContext.SaveChangesAsync();

        var idleStart = DateTime.UtcNow;
        var refreshResult = await testContext.AuthService.RefreshTokenAsync(
            new RefreshTokenRequest("refresh-token-0", IpAddress, UserAgent));

        Assert.False(refreshResult.IsSuccess);
        Assert.Equal("Session expired due to inactivity.", refreshResult.Message);
        Assert.Equal("SessionIdleTimeout", refreshResult.ErrorCode);
        Assert.Null(refreshResult.AccessToken);
        Assert.Null(refreshResult.RefreshToken);

        var revoked = await testContext.DbContext.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.NotNull(revoked.RevokedAt);
        Assert.InRange(revoked.RevokedAt!.Value, idleStart.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(IpAddress, revoked.RevokedByIp);

        var idleLog = await testContext.DbContext.SecurityLogs
            .Where(x => x.EventType == "SessionIdleTimeout")
            .SingleAsync();
        Assert.Equal("Info", idleLog.Severity);
        Assert.Equal("Session expired due to inactivity.", idleLog.Description);
        Assert.Equal(user.Id, idleLog.UserId);
        Assert.Equal(user.UserName, idleLog.UserName);
        Assert.Equal(IpAddress, idleLog.IpAddress);
        Assert.Equal(UserAgent, idleLog.UserAgent);
        Assert.DoesNotContain("refresh-token", idleLog.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenWithinIdleWindow_RotatesTokenAndUpdatesLastUsedAt()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        testContext.SettingsService.SessionSecurity = new SessionSecuritySettings(30, 30, 30, 6, 7, true);
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        var user = await SeedPortalUserWithRolePermissionAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile(user.DirectoryObjectId, user.UserName);

        var loginResult = await testContext.AuthService.LoginAsync(
            new LoginRequest(user.UserName, "password", IpAddress, UserAgent, RememberMe: true));
        Assert.True(loginResult.IsSuccess);

        var issued = await testContext.DbContext.RefreshTokens.SingleAsync();
        var absoluteExpiry = issued.ExpiresAt;
        var originalLastUsedAt = DateTime.UtcNow.AddMinutes(-5);
        issued.LastUsedAt = originalLastUsedAt;
        await testContext.DbContext.SaveChangesAsync();

        var refreshStart = DateTime.UtcNow;
        var refreshResult = await testContext.AuthService.RefreshTokenAsync(
            new RefreshTokenRequest("refresh-token-0", IpAddress, UserAgent));

        Assert.True(refreshResult.IsSuccess);
        Assert.Null(refreshResult.ErrorCode);
        Assert.Equal(absoluteExpiry, refreshResult.RefreshTokenExpiresAt);

        var active = await testContext.DbContext.RefreshTokens
            .AsNoTracking()
            .SingleAsync(x => x.RevokedAt == null);
        Assert.Equal(absoluteExpiry, active.ExpiresAt);
        Assert.True(active.IsPersistent);
        Assert.InRange(active.LastUsedAt, refreshStart.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task RefreshTokenAsync_PreservesIsPersistentAndAbsoluteExpiry_AndSetsLastUsedAtOnNewToken()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        var user = await SeedPortalUserWithRolePermissionAsync(testContext.DbContext);
        testContext.LdapService.ValidateResult = new LdapValidationResult(true, "ok");
        testContext.LdapService.UserProfileResult = CreateLdapProfile(user.DirectoryObjectId, user.UserName);

        var loginResult = await testContext.AuthService.LoginAsync(
            new LoginRequest(user.UserName, "password", IpAddress, UserAgent, RememberMe: true));

        Assert.True(loginResult.IsSuccess);
        var issued = await testContext.DbContext.RefreshTokens.AsNoTracking().SingleAsync();
        var absoluteExpiry = issued.ExpiresAt;
        var issuedLastUsed = issued.LastUsedAt;

        var refreshResult = await testContext.AuthService.RefreshTokenAsync(
            new RefreshTokenRequest("refresh-token-0", IpAddress, UserAgent));

        Assert.True(refreshResult.IsSuccess);
        Assert.Equal(absoluteExpiry, refreshResult.RefreshTokenExpiresAt);

        var active = await testContext.DbContext.RefreshTokens.SingleAsync(x => x.RevokedAt == null);
        Assert.Equal(absoluteExpiry, active.ExpiresAt);
        Assert.True(active.IsPersistent);
        Assert.True(active.LastUsedAt >= issuedLastUsed);
        Assert.Equal("hash:refresh-token-1", active.TokenHash);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenMissing_ReturnsFailure_AndDoesNotWriteSecurityLog()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();

        var result = await testContext.AuthService.RefreshTokenAsync(new RefreshTokenRequest("", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Refresh token is required.", result.Message);
        Assert.Empty(await testContext.DbContext.SecurityLogs.ToListAsync());
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenInvalid_WritesRefreshTokenFailedWarning()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();

        var result = await testContext.AuthService.RefreshTokenAsync(new RefreshTokenRequest("invalid-refresh-token", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid refresh token.", result.Message);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("RefreshTokenFailed", log.EventType);
        Assert.Equal("Warning", log.Severity);
        Assert.Equal("Invalid refresh token.", log.Description);
        Assert.DoesNotContain("invalid-refresh-token", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogoutAsync_WhenRefreshTokenMissing_ReturnsFailure_AndDoesNotWriteSecurityLog()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();

        var result = await testContext.AuthService.LogoutAsync(new LogoutRequest("", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Refresh token is required.", result.Message);
        Assert.Empty(await testContext.DbContext.SecurityLogs.ToListAsync());
    }

    [Fact]
    public async Task LogoutAsync_WhenRefreshTokenInvalid_WritesLogoutWarning()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();

        var result = await testContext.AuthService.LogoutAsync(new LogoutRequest("invalid-refresh-token", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid refresh token.", result.Message);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("Logout", log.EventType);
        Assert.Equal("Warning", log.Severity);
        Assert.Equal("Logout failed. Refresh token was not found.", log.Description);
        Assert.DoesNotContain("invalid-refresh-token", log.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_WhenUnexpectedException_WritesLoginError()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        await SeedActiveLdapSettingAsync(testContext.DbContext);
        testContext.LdapService.ValidateException = new InvalidOperationException("unexpected-ldap-error");

        var result = await testContext.AuthService.LoginAsync(new LoginRequest("mete.user", "password", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Login could not be completed.", result.Message);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LoginError", log.EventType);
        Assert.Equal("Error", log.Severity);
        Assert.Equal("Login could not be completed because an unexpected error occurred.", log.Description);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenUnexpectedException_WritesRefreshTokenError()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        testContext.TokenService.HashRefreshTokenException = new InvalidOperationException("unexpected-token-hash-error");

        var result = await testContext.AuthService.RefreshTokenAsync(new RefreshTokenRequest("refresh-token", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Token refresh could not be completed.", result.Message);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("RefreshTokenError", log.EventType);
        Assert.Equal("Error", log.Severity);
        Assert.Equal("Token refresh could not be completed because an unexpected error occurred.", log.Description);
    }

    [Fact]
    public async Task LogoutAsync_WhenUnexpectedException_WritesLogoutError()
    {
        await using var testContext = await AuthServiceTestFactory.CreateAsync();
        testContext.TokenService.HashRefreshTokenException = new InvalidOperationException("unexpected-token-hash-error");

        var result = await testContext.AuthService.LogoutAsync(new LogoutRequest("refresh-token", IpAddress, UserAgent));

        Assert.False(result.IsSuccess);
        Assert.Equal("Logout could not be completed.", result.Message);
        var log = await AssertSingleSecurityLogAsync(testContext.DbContext);
        Assert.Equal("LogoutError", log.EventType);
        Assert.Equal("Error", log.Severity);
        Assert.Equal("Logout could not be completed because an unexpected error occurred.", log.Description);
    }

    private static async Task<SecurityLog> AssertSingleSecurityLogAsync(AppDbContext context)
    {
        var logs = await context.SecurityLogs.ToListAsync();
        var log = Assert.Single(logs);
        Assert.Equal(IpAddress, log.IpAddress);
        Assert.Equal(UserAgent, log.UserAgent);
        return log;
    }

    private static async Task SeedActiveLdapSettingAsync(AppDbContext context)
    {
        await context.LdapSettings.AddAsync(new LdapSetting
        {
            Name = "Primary LDAP",
            Host = "ldap.test.local",
            Port = 389,
            UseSsl = false,
            BaseDn = "DC=test,DC=local",
            UserSearchBase = "OU=Users,DC=test,DC=local",
            UserSearchFilter = "(sAMAccountName={0})",
            BindUserName = "svc-ldap",
            BindUserDomain = "TEST",
            EncryptedBindPassword = "protected:bind-secret",
            IsActive = true,
            IsDeleted = false
        });

        await context.SaveChangesAsync();
    }

    private static async Task<PortalUser> SeedPortalUserAsync(
        AppDbContext context,
        bool isActive)
    {
        var user = new PortalUser
        {
            DirectorySource = "ActiveDirectory",
            DirectoryObjectId = "directory-object-id-1",
            UserName = "mete.user",
            DisplayName = "Mete User",
            Email = "mete.user@test.local",
            IsActive = isActive,
            IsDeleted = false
        };

        await context.PortalUsers.AddAsync(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<PortalUser> SeedPortalUserWithRolePermissionAsync(AppDbContext context)
    {
        var user = new PortalUser
        {
            DirectorySource = "ActiveDirectory",
            DirectoryObjectId = "directory-object-id-2",
            UserName = "mete.user",
            DisplayName = "Mete User",
            Email = "mete.user@test.local",
            IsActive = true,
            IsDeleted = false
        };

        var role = new PortalRole
        {
            Name = "Operator",
            Code = "Operator",
            IsActive = true,
            IsDeleted = false
        };

        var permission = new PortalPermission
        {
            Module = "Auth",
            Code = "Auth.Login",
            IsActive = true,
            IsDeleted = false
        };

        var userRole = new PortalUserRole
        {
            PortalUser = user,
            PortalRole = role,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };

        var rolePermission = new PortalRolePermission
        {
            PortalRole = role,
            PortalPermission = permission,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };

        await context.PortalUsers.AddAsync(user);
        await context.PortalRoles.AddAsync(role);
        await context.PortalPermissions.AddAsync(permission);
        await context.PortalUserRoles.AddAsync(userRole);
        await context.PortalRolePermissions.AddAsync(rolePermission);
        await context.ApplicationSettings.AddAsync(new ApplicationSetting
        {
            Key = "Directory:NationalIdAttribute",
            Value = "employeeId",
            ValueType = SettingValueType.String,
            IsActive = true,
            IsDeleted = false
        });

        await context.SaveChangesAsync();
        return user;
    }

    private static LdapUserProfile CreateLdapProfile(
        string directoryObjectId = "directory-object-id-1",
        string userName = "mete.user")
        => new(
            directoryObjectId,
            userName,
            "Mete User",
            "mete.user@test.local",
            "12345678901");
}
