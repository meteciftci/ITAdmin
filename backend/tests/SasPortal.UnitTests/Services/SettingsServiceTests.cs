using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.Fakes;

namespace SasPortal.UnitTests.Services;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task GetSettingsAsync_ReturnsActiveLdapAndHidesEncryptedApplicationValues()
    {
        await using var dbContext = CreateDbContext();
        await SeedActiveLdapAsync(dbContext, "protected:topsecret");

        await dbContext.ApplicationSettings.AddRangeAsync(
            new ApplicationSetting
            {
                Key = "Directory:NationalIdAttribute",
                Value = "employeeId",
                ValueType = SettingValueType.String,
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            },
            new ApplicationSetting
            {
                Key = "Some:Encrypted",
                Value = "protected:value",
                ValueType = SettingValueType.String,
                IsEncrypted = true,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetSettingsAsync();

        Assert.NotNull(result.Ldap);
        Assert.True(result.Ldap!.HasBindPassword);
        Assert.DoesNotContain("topsecret", result.Ldap.BindUserName, StringComparison.Ordinal);

        var encrypted = Assert.Single(result.ApplicationSettings, x => x.Key == "Some:Encrypted");
        Assert.True(encrypted.IsEncrypted);
        Assert.Null(encrypted.Value);

        var plain = Assert.Single(result.ApplicationSettings, x => x.Key == "Directory:NationalIdAttribute");
        Assert.False(plain.IsEncrypted);
        Assert.Equal("employeeId", plain.Value);
    }

    [Fact]
    public async Task UpdateLdapSettingsAsync_WithEmptyBindPassword_KeepsExistingSecret_AndWritesSafeAudit()
    {
        await using var dbContext = CreateDbContext();
        var ldapSetting = await SeedActiveLdapAsync(dbContext, "protected:current-secret");

        var service = CreateService(dbContext);
        var request = new UpdateLdapSettingsRequest(
            Name: "LDAP-Updated",
            Host: "ldap.updated.local",
            Port: 636,
            UseSsl: true,
            BaseDn: "DC=updated,DC=local",
            UserSearchBase: "OU=Users,DC=updated,DC=local",
            UserSearchFilter: "(sAMAccountName={0})",
            BindUserName: "svc_updated",
            BindUserDomain: "UPDATED",
            BindPassword: " ",
            Description: "updated",
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "127.0.0.1",
            ActorUserAgent: "xunit");

        var result = await service.UpdateLdapSettingsAsync(request);

        Assert.True(result.IsSuccess);

        var updated = await dbContext.LdapSettings.SingleAsync(x => x.Id == ldapSetting.Id);
        Assert.Equal("protected:current-secret", updated.EncryptedBindPassword);

        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "LdapSetting"));
        Assert.NotNull(audit.Description);
        Assert.DoesNotContain("current-secret", audit.Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("protected:current-secret", audit.Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("Bind password changed.", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateLdapSettingsAsync_WithNewBindPassword_StoresProtectedSecret_AndWritesSafeAudit()
    {
        await using var dbContext = CreateDbContext();
        var ldapSetting = await SeedActiveLdapAsync(dbContext, "protected:old-secret");

        var service = CreateService(dbContext);
        var request = new UpdateLdapSettingsRequest(
            Name: "LDAP-Updated",
            Host: "ldap.updated.local",
            Port: 636,
            UseSsl: true,
            BaseDn: "DC=updated,DC=local",
            UserSearchBase: "OU=Users,DC=updated,DC=local",
            UserSearchFilter: "(sAMAccountName={0})",
            BindUserName: "svc_updated",
            BindUserDomain: "UPDATED",
            BindPassword: "new-secret",
            Description: "updated",
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "127.0.0.1",
            ActorUserAgent: "xunit");

        var result = await service.UpdateLdapSettingsAsync(request);

        Assert.True(result.IsSuccess);

        var updated = await dbContext.LdapSettings.SingleAsync(x => x.Id == ldapSetting.Id);
        Assert.Equal("protected:new-secret", updated.EncryptedBindPassword);

        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "LdapSetting"));
        Assert.NotNull(audit.Description);
        Assert.Contains("Bind password changed.", audit.Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("new-secret", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_RejectsKeyOutsideAllowlist()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest("Not:Allowed", "x", SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("not allowed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(dbContext.ApplicationSettings);
        Assert.Empty(dbContext.AuditLogs);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_UpsertsNationalIdAttribute_AndWritesSafeAudit()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest("Directory:NationalIdAttribute", "employeeId", SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.True(result.IsSuccess);

        var setting = Assert.Single(dbContext.ApplicationSettings);
        Assert.Equal("Directory:NationalIdAttribute", setting.Key);
        Assert.Equal("employeeId", setting.Value);
        Assert.Equal(SettingValueType.String, setting.ValueType);

        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "ApplicationSetting"));
        Assert.NotNull(audit.Description);
        Assert.DoesNotContain("employeeId", audit.Description!, StringComparison.Ordinal);
        Assert.Contains("Value changed.", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBrandingSettingsAsync_ReturnsDefaults_WhenSettingsDoNotExist()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var branding = await service.GetBrandingSettingsAsync();

        Assert.Equal("SAS Portal v2", branding.ApplicationName);
        Assert.Equal("SAS Portal v2", branding.BrowserTitle);
        Assert.Null(branding.LogoUrl);
        Assert.Equal("/favicon.svg", branding.FaviconUrl);
        Assert.Equal("https://sifre.mugla.bel.tr", branding.ForgotPasswordUrl);
    }

    [Fact]
    public async Task GetBrandingSettingsAsync_ReturnsPersistedBrandingValues()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddRangeAsync(
            new ApplicationSetting
            {
                Key = "Branding:ApplicationName",
                Value = "Portal Name",
                ValueType = SettingValueType.String,
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            },
            new ApplicationSetting
            {
                Key = "Branding:BrowserTitle",
                Value = "Portal Browser Title",
                ValueType = SettingValueType.String,
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            },
            new ApplicationSetting
            {
                Key = "Branding:LogoUrl",
                Value = "/uploads/branding/logo.png",
                ValueType = SettingValueType.String,
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            },
            new ApplicationSetting
            {
                Key = "Branding:FaviconUrl",
                Value = "/uploads/branding/favicon.png",
                ValueType = SettingValueType.String,
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            },
            new ApplicationSetting
            {
                Key = "Branding:ForgotPasswordUrl",
                Value = "https://reset.example.com",
                ValueType = SettingValueType.String,
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var branding = await service.GetBrandingSettingsAsync();

        Assert.Equal("Portal Name", branding.ApplicationName);
        Assert.Equal("Portal Browser Title", branding.BrowserTitle);
        Assert.Equal("/uploads/branding/logo.png", branding.LogoUrl);
        Assert.Equal("/uploads/branding/favicon.png", branding.FaviconUrl);
        Assert.Equal("https://reset.example.com", branding.ForgotPasswordUrl);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_RejectsInvalidBrandingLogoUrl()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest("Branding:LogoUrl", "javascript:alert(1)", SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("http/https URL", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(dbContext.ApplicationSettings);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_BrandingAuditDescription_DoesNotContainValue()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest("Branding:ApplicationName", "SAS Secret Portal Name", SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "ApplicationSetting"));
        Assert.NotNull(audit.Description);
        Assert.DoesNotContain("SAS Secret Portal Name", audit.Description!, StringComparison.Ordinal);
        Assert.Contains("Branding:ApplicationName", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_RejectsInvalidBrandingFaviconUrl()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest("Branding:FaviconUrl", "javascript:alert(1)", SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("http/https URL", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(dbContext.ApplicationSettings);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_RejectsInvalidBrandingForgotPasswordUrl()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest(
                    "Branding:ForgotPasswordUrl",
                    "/relative/forgot",
                    SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("http/https URL", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(dbContext.ApplicationSettings);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_RejectsForgotPasswordUrlWithUnsafeScheme()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest(
                    "Branding:ForgotPasswordUrl",
                    "javascript:alert(1)",
                    SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Empty(dbContext.ApplicationSettings);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WithEmptyBindPasswordAndExistingSetting_UsesUnprotectedStoredSecret()
    {
        await using var dbContext = CreateDbContext();
        await SeedActiveLdapAsync(dbContext, "protected:persisted-secret");
        var ldapService = new FakeLdapService();
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: " ", testUserName: null, testPassword: null);
        var result = await service.ValidateLdapSettingsAsync(request);

        Assert.True(result.IsValid);
        Assert.Equal(1, ldapService.ValidateBindCallCount);
        Assert.Equal("persisted-secret", ldapService.LastValidateBindRequest!.BindPassword);
        Assert.Equal(0, ldapService.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WithEmptyBindPasswordAndNoExistingSetting_ReturnsFalse()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateValidateRequest(bindPassword: " ", testUserName: null, testPassword: null);
        var result = await service.ValidateLdapSettingsAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WithoutTestCredentials_CallsValidateBindAsync()
    {
        await using var dbContext = CreateDbContext();
        var ldapService = new FakeLdapService();
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: "plain-secret", testUserName: " ", testPassword: " ");
        await service.ValidateLdapSettingsAsync(request);

        Assert.Equal(1, ldapService.ValidateBindCallCount);
        Assert.Equal(0, ldapService.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WithTestCredentials_CallsValidateAsync()
    {
        await using var dbContext = CreateDbContext();
        var ldapService = new FakeLdapService();
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: "plain-secret", testUserName: "john", testPassword: "pw");
        await service.ValidateLdapSettingsAsync(request);

        Assert.Equal(0, ldapService.ValidateBindCallCount);
        Assert.Equal(1, ldapService.ValidateCallCount);
        Assert.Equal("john", ldapService.LastValidateRequest!.TestUserName);
    }

    [Fact]
    public async Task GetSettingsAsync_IncludesSessionSecurityDefaults_WhenNoSecurityRows()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var result = await service.GetSettingsAsync();

        Assert.Equal(30, result.SessionSecurity.AccessTokenMinutes);
        Assert.Equal(30, result.SessionSecurity.IdleTimeoutMinutes);
        Assert.Equal(30, result.SessionSecurity.IdleWarningSeconds);
        Assert.Equal(6, result.SessionSecurity.SessionRefreshTokenHours);
        Assert.Equal(7, result.SessionSecurity.RememberMeRefreshTokenDays);
        Assert.True(result.SessionSecurity.RememberMeEnabled);
    }

    [Fact]
    public async Task GetAuthSessionOptionsAsync_ReturnsDefaults_WhenNoSecurityRows()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var options = await service.GetAuthSessionOptionsAsync();

        Assert.True(options.RememberMeEnabled);
        Assert.Equal(30, options.IdleTimeoutMinutes);
        Assert.Equal(30, options.IdleWarningSeconds);
        Assert.Equal(30, options.AccessTokenMinutes);
    }

    [Fact]
    public async Task GetAuthSessionOptionsAsync_ReturnsConfiguredIdleSettings()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateSessionSecurityRequest(
            accessTokenMinutes: 15,
            idleTimeoutMinutes: 45,
            idleWarningSeconds: 60,
            rememberMeEnabled: false);
        var updateResult = await service.UpdateSessionSecuritySettingsAsync(request);
        Assert.True(updateResult.IsSuccess);

        var options = await service.GetAuthSessionOptionsAsync();

        Assert.False(options.RememberMeEnabled);
        Assert.Equal(45, options.IdleTimeoutMinutes);
        Assert.Equal(60, options.IdleWarningSeconds);
        Assert.Equal(15, options.AccessTokenMinutes);
    }

    [Fact]
    public async Task UpdateSessionSecuritySettingsAsync_WhenSecurityRowsMissingAndValuesAreDefaults_UpsertsRows()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateSessionSecurityRequest();
        var result = await service.UpdateSessionSecuritySettingsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Session security settings initialized.", result.Message);

        var securityRows = await dbContext.ApplicationSettings
            .Where(x => !x.IsDeleted && x.IsActive && SecuritySettingKeys.AllSet.Contains(x.Key))
            .ToListAsync();

        Assert.Equal(6, securityRows.Count);
        Assert.Equal("30", securityRows.Single(x => x.Key == SecuritySettingKeys.AccessTokenMinutes).Value);
        Assert.Equal("30", securityRows.Single(x => x.Key == SecuritySettingKeys.IdleTimeoutMinutes).Value);
        Assert.Equal("30", securityRows.Single(x => x.Key == SecuritySettingKeys.IdleWarningSeconds).Value);
        Assert.Equal("6", securityRows.Single(x => x.Key == SecuritySettingKeys.SessionRefreshTokenHours).Value);
        Assert.Equal("7", securityRows.Single(x => x.Key == SecuritySettingKeys.RememberMeRefreshTokenDays).Value);
        Assert.Equal("true", securityRows.Single(x => x.Key == SecuritySettingKeys.RememberMeEnabled).Value);

        foreach (var row in securityRows)
        {
            Assert.True(row.IsSystem);
            Assert.True(row.IsActive);
            Assert.False(row.IsEncrypted);
        }

        var audits = await dbContext.AuditLogs.Where(x => x.EntityName == "SessionSecuritySettings").ToListAsync();
        Assert.Single(audits);
        Assert.Contains("initialized", audits[0].Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSessionSecuritySettingsAsync_WhenRowsExistAndValuesUnchanged_DoesNotWriteSecondAudit()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateSessionSecuritySettingsAsync(CreateSessionSecurityRequest());

        var second = await service.UpdateSessionSecuritySettingsAsync(CreateSessionSecurityRequest());

        Assert.True(second.IsSuccess);
        Assert.Equal("Session security settings are unchanged.", second.Message);

        var auditCount = await dbContext.AuditLogs.CountAsync(x => x.EntityName == "SessionSecuritySettings");
        Assert.Equal(1, auditCount);

        var securityCount = await dbContext.ApplicationSettings.CountAsync(x =>
            !x.IsDeleted && x.IsActive && SecuritySettingKeys.AllSet.Contains(x.Key));
        Assert.Equal(6, securityCount);
    }

    [Fact]
    public async Task UpdateSessionSecuritySettingsAsync_RejectsAccessTokenOutOfRange()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = CreateSessionSecurityRequest(accessTokenMinutes: 4);

        var result = await service.UpdateSessionSecuritySettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("5 and 240", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSessionSecuritySettingsAsync_RejectsAccessTokenGreaterThanIdleTimeout()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = CreateSessionSecurityRequest(accessTokenMinutes: 60, idleTimeoutMinutes: 30);

        var result = await service.UpdateSessionSecuritySettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Access token duration cannot be greater than idle timeout.", result.Message);
        Assert.Empty(dbContext.ApplicationSettings);
        Assert.Empty(dbContext.AuditLogs);
    }

    [Fact]
    public async Task UpdateSessionSecuritySettingsAsync_RejectsIdleWarningAgainstIdleTimeout()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = CreateSessionSecurityRequest(
            accessTokenMinutes: 5,
            idleTimeoutMinutes: 5,
            idleWarningSeconds: 300);

        var result = await service.UpdateSessionSecuritySettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("less than the idle timeout", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSessionSecuritySettingsAsync_UpsertsValues_AndSkipsAuditWhenUnchanged()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var first = CreateSessionSecurityRequest(accessTokenMinutes: 45, idleTimeoutMinutes: 45);
        var firstResult = await service.UpdateSessionSecuritySettingsAsync(first);
        Assert.True(firstResult.IsSuccess);
        Assert.Equal(45, firstResult.Settings!.SessionSecurity.AccessTokenMinutes);

        var accessRow = await dbContext.ApplicationSettings.SingleAsync(x => x.Key == SecuritySettingKeys.AccessTokenMinutes);
        Assert.Equal("45", accessRow.Value);
        Assert.Equal(SettingValueType.Number, accessRow.ValueType);

        var duplicate = CreateSessionSecurityRequest(accessTokenMinutes: 45, idleTimeoutMinutes: 45);
        var secondResult = await service.UpdateSessionSecuritySettingsAsync(duplicate);
        Assert.True(secondResult.IsSuccess);
        Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "SessionSecuritySettings"));

        var third = CreateSessionSecurityRequest(accessTokenMinutes: 60, idleTimeoutMinutes: 60);
        await service.UpdateSessionSecuritySettingsAsync(third);
        var audits = await dbContext.AuditLogs.Where(x => x.EntityName == "SessionSecuritySettings").ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Contains("AccessTokenMinutes: 45 -> 60", audits[^1].Description, StringComparison.Ordinal);
    }

    private static UpdateSessionSecuritySettingsRequest CreateSessionSecurityRequest(
        int? accessTokenMinutes = null,
        int? idleTimeoutMinutes = null,
        int? idleWarningSeconds = null,
        int? sessionRefreshTokenHours = null,
        int? rememberMeRefreshTokenDays = null,
        bool? rememberMeEnabled = null) =>
        new(
            accessTokenMinutes ?? 30,
            idleTimeoutMinutes ?? 30,
            idleWarningSeconds ?? 30,
            sessionRefreshTokenHours ?? 6,
            rememberMeRefreshTokenDays ?? 7,
            rememberMeEnabled ?? true,
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static SettingsService CreateService(AppDbContext context, FakeLdapService? ldapService = null)
        => new(
            context,
            ldapService ?? new FakeLdapService(),
            new FakeSecretProtector(),
            NullLogger<SettingsService>.Instance);

    private static async Task<LdapSetting> SeedActiveLdapAsync(AppDbContext context, string encryptedBindPassword)
    {
        var ldap = new LdapSetting
        {
            Name = "LDAP",
            Host = "ldap.local",
            Port = 389,
            UseSsl = false,
            BaseDn = "DC=local,DC=test",
            UserSearchBase = "OU=Users,DC=local,DC=test",
            UserSearchFilter = "(sAMAccountName={0})",
            BindUserName = "svc_bind",
            BindUserDomain = "LOCAL",
            EncryptedBindPassword = encryptedBindPassword,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        await context.LdapSettings.AddAsync(ldap);
        await context.SaveChangesAsync();
        return ldap;
    }

    private static ValidateLdapSettingsRequest CreateValidateRequest(
        string? bindPassword,
        string? testUserName,
        string? testPassword)
        => new(
            Name: "LDAP",
            Host: "ldap.local",
            Port: 389,
            UseSsl: false,
            BaseDn: "DC=local,DC=test",
            UserSearchBase: "OU=Users,DC=local,DC=test",
            UserSearchFilter: "(sAMAccountName={0})",
            BindUserName: "svc_bind",
            BindUserDomain: "LOCAL",
            BindPassword: bindPassword,
            TestUserName: testUserName,
            TestPassword: testPassword,
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "127.0.0.1",
            ActorUserAgent: "xunit");

}
