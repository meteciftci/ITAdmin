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
    public async Task UpdateApplicationSettingsAsync_UpsertsNationalIdAttribute_AndWritesDiffAudit()
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
        Assert.Contains("Directory:NationalIdAttribute", audit.Description!, StringComparison.Ordinal);
        Assert.Contains("Value: <none> -> employeeId.", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_NationalIdAttribute_IncludesPreviousValueInAudit()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(new ApplicationSetting
        {
            Key = "Directory:NationalIdAttribute",
            Value = "sAMAccountName",
            ValueType = SettingValueType.String,
            IsEncrypted = false,
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

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
        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "ApplicationSetting"));
        Assert.NotNull(audit.Description);
        Assert.Contains("Value: sAMAccountName -> employeeId.", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_UnchangedValue_DoesNotWriteAudit()
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

        var first = await service.UpdateApplicationSettingsAsync(request);
        Assert.True(first.IsSuccess);
        Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "ApplicationSetting"));

        var second = await service.UpdateApplicationSettingsAsync(request);
        Assert.True(second.IsSuccess);

        var audits = await dbContext.AuditLogs
            .Where(x => x.EntityName == "ApplicationSetting")
            .ToListAsync();
        Assert.Single(audits);
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
        Assert.Null(branding.ForgotPasswordUrl);
        Assert.Equal($"© {DateTime.UtcNow.Year} SAS Portal", branding.FooterText);
    }

    [Fact]
    public async Task GetBrandingSettingsAsync_FooterText_ReturnsFallback_WhenStoredEmptyOrWhitespace()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(
            new ApplicationSetting
            {
                Key = "Branding:FooterText",
                Value = "   ",
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

        Assert.Equal($"© {DateTime.UtcNow.Year} SAS Portal", branding.FooterText);
    }

    [Fact]
    public async Task GetBrandingSettingsAsync_FooterText_ReturnsPersistedValue_WhenConfigured()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(
            new ApplicationSetting
            {
                Key = "Branding:FooterText",
                Value = "© 2026 Muğla Büyükşehir Belediyesi - SAS Portal",
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

        Assert.Equal("© 2026 Muğla Büyükşehir Belediyesi - SAS Portal", branding.FooterText);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_RejectsFooterText_WhenExceedsMaxLength()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.UpdateApplicationSettingsAsync(
            new UpdateApplicationSettingsRequest(
                [
                    new UpdateApplicationSettingRequest(
                        "Branding:FooterText",
                        new string('x', 201),
                        SettingValueType.String)
                ],
                Guid.NewGuid(),
                "admin",
                null,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("200", result.Message!, StringComparison.Ordinal);
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
    public async Task GetBrandingSettingsAsync_ForgotPasswordUrl_IsNull_WhenStoredEmptyOrWhitespace()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(
            new ApplicationSetting
            {
                Key = "Branding:ForgotPasswordUrl",
                Value = "   ",
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

        Assert.Null(branding.ForgotPasswordUrl);
    }

    [Fact]
    public async Task GetBrandingSettingsAsync_ForgotPasswordUrl_IsNull_WhenStoredValueIsInvalidUrl()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(
            new ApplicationSetting
            {
                Key = "Branding:ForgotPasswordUrl",
                Value = "not-a-valid-url",
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

        Assert.Null(branding.ForgotPasswordUrl);
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
    public async Task UpdateApplicationSettingsAsync_BrandingApplicationName_AuditIncludesOldAndNewValue()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(new ApplicationSetting
        {
            Key = "Branding:ApplicationName",
            Value = "SAS Portal v2",
            ValueType = SettingValueType.String,
            IsEncrypted = false,
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest("Branding:ApplicationName", "SAS Portal", SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "ApplicationSetting"));
        Assert.NotNull(audit.Description);
        Assert.Contains("Branding:ApplicationName", audit.Description!, StringComparison.Ordinal);
        Assert.Contains("Value: SAS Portal v2 -> SAS Portal.", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_BrandingFaviconUrl_AuditIncludesOldAndNewValue()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(new ApplicationSetting
        {
            Key = "Branding:FaviconUrl",
            Value = "/favicon.svg",
            ValueType = SettingValueType.String,
            IsEncrypted = false,
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest(
                    "Branding:FaviconUrl",
                    "/uploads/branding/favicon.ico",
                    SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "ApplicationSetting"));
        Assert.NotNull(audit.Description);
        Assert.Contains(
            "Value: /favicon.svg -> /uploads/branding/favicon.ico.",
            audit.Description!,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_BrandingForgotPasswordUrl_AuditIncludesNewValueFromNone()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest(
                    "Branding:ForgotPasswordUrl",
                    "https://reset.example.com",
                    SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "ApplicationSetting"));
        Assert.NotNull(audit.Description);
        Assert.Contains(
            "Value: <none> -> https://reset.example.com.",
            audit.Description!,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Directory:NationalIdAttribute", "employeeId", "LDAP attribute name that stores the national identity value.")]
    [InlineData("Branding:ApplicationName", "SAS Portal", "Application display name.")]
    [InlineData("Branding:BrowserTitle", "Portal", "Browser title shown in the tab.")]
    [InlineData("Branding:LogoUrl", "/uploads/branding/logo.png", "Application branding logo URL.")]
    [InlineData("Branding:FaviconUrl", "/uploads/branding/favicon.png", "Application branding favicon URL.")]
    [InlineData("Branding:ForgotPasswordUrl", "https://reset.example.com", "External forgot password URL shown on the login page.")]
    [InlineData("Branding:FooterText", "© 2026 SAS Portal", "Footer text shown centered at the bottom of the application layout.")]
    public async Task UpdateApplicationSettingsAsync_Create_UsesKeySpecificDescription(
        string key,
        string value,
        string expectedDescription)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest(key, value, SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.True(result.IsSuccess);
        var setting = Assert.Single(dbContext.ApplicationSettings);
        Assert.Equal(key, setting.Key);
        Assert.Equal(expectedDescription, setting.Description);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_Update_HealsIncorrectBrandingDescription()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(new ApplicationSetting
        {
            Key = "Branding:LogoUrl",
            Value = "/uploads/branding/old.png",
            ValueType = SettingValueType.String,
            Description = "LDAP attribute name that stores the national identity value.",
            IsEncrypted = false,
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest(
                    "Branding:LogoUrl",
                    "/uploads/branding/new.png",
                    SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.True(result.IsSuccess);
        var setting = Assert.Single(dbContext.ApplicationSettings);
        Assert.Equal("Application branding logo URL.", setting.Description);
        Assert.Equal("/uploads/branding/new.png", setting.Value);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_RejectsSensitiveSuffixKey_BeforeWritingValue()
    {
        // Defense-in-depth: even if a future sensitive key (e.g. "Smtp:Password") is mistakenly
        // submitted through this endpoint, the allowlist must reject it so its value is never
        // persisted nor audited. This protects the audit formatter from receiving secrets.
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest("Smtp:Password", "super-secret", SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Empty(dbContext.ApplicationSettings);
        Assert.Empty(dbContext.AuditLogs);
    }

    [Fact]
    public async Task UpdateApplicationSettingsAsync_EncryptedSetting_DoesNotLeakValueIntoAudit()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.ApplicationSettings.AddAsync(new ApplicationSetting
        {
            Key = "Branding:ApplicationName",
            Value = "protected:legacy",
            ValueType = SettingValueType.String,
            IsEncrypted = true,
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = new UpdateApplicationSettingsRequest(
            new[]
            {
                new UpdateApplicationSettingRequest("Branding:ApplicationName", "SAS Portal", SettingValueType.String)
            },
            Guid.NewGuid(),
            "tester",
            "127.0.0.1",
            "xunit");

        var result = await service.UpdateApplicationSettingsAsync(request);

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "ApplicationSetting"));
        Assert.NotNull(audit.Description);
        Assert.DoesNotContain("protected:legacy", audit.Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("SAS Portal", audit.Description!, StringComparison.Ordinal);
        Assert.Contains("Value changed.", audit.Description!, StringComparison.Ordinal);
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
        Assert.Equal(1, ldapService.ValidateSearchBasesCallCount);
        Assert.Equal("persisted-secret", ldapService.LastValidateSearchBasesRequest!.BindPassword);
        Assert.Equal(0, ldapService.ValidateBindCallCount);
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
    public async Task ValidateLdapSettingsAsync_WithoutTestCredentials_CallsValidateSearchBasesAsync()
    {
        await using var dbContext = CreateDbContext();
        var ldapService = new FakeLdapService();
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: "plain-secret", testUserName: " ", testPassword: " ");
        await service.ValidateLdapSettingsAsync(request);

        Assert.Equal(1, ldapService.ValidateSearchBasesCallCount);
        Assert.Equal(0, ldapService.ValidateBindCallCount);
        Assert.Equal(0, ldapService.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WithTestCredentials_CallsValidateSearchBasesThenValidateAsync()
    {
        await using var dbContext = CreateDbContext();
        var ldapService = new FakeLdapService();
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: "plain-secret", testUserName: "john", testPassword: "pw");
        await service.ValidateLdapSettingsAsync(request);

        Assert.Equal(1, ldapService.ValidateSearchBasesCallCount);
        Assert.Equal(0, ldapService.ValidateBindCallCount);
        Assert.Equal(1, ldapService.ValidateCallCount);
        Assert.Equal("john", ldapService.LastValidateRequest!.TestUserName);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WhenSearchBasesValidationFails_ReturnsFalse()
    {
        await using var dbContext = CreateDbContext();
        var ldapService = new FakeLdapService
        {
            ValidateSearchBasesResult = new(false, "LDAP base DN could not be resolved.")
        };
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: "plain-secret", testUserName: " ", testPassword: " ");
        var result = await service.ValidateLdapSettingsAsync(request);

        Assert.False(result.IsValid);
        Assert.Equal("LDAP base DN could not be resolved.", result.Message);
        Assert.Equal(1, ldapService.ValidateSearchBasesCallCount);
        Assert.Equal(0, ldapService.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WhenUserSearchBaseValidationFails_ReturnsFalse()
    {
        await using var dbContext = CreateDbContext();
        var ldapService = new FakeLdapService
        {
            ValidateSearchBasesResult = new(false, "LDAP user search base could not be resolved.")
        };
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: "plain-secret", testUserName: " ", testPassword: " ");
        var result = await service.ValidateLdapSettingsAsync(request);

        Assert.False(result.IsValid);
        Assert.Equal("LDAP user search base could not be resolved.", result.Message);
        Assert.Equal(1, ldapService.ValidateSearchBasesCallCount);
        Assert.Equal(0, ldapService.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WhenSearchBasesFails_WithTestCredentials_DoesNotCallValidateAsync()
    {
        await using var dbContext = CreateDbContext();
        var ldapService = new FakeLdapService
        {
            ValidateSearchBasesResult = new(false, "LDAP service account authentication failed.")
        };
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: "plain-secret", testUserName: "john", testPassword: "pw");
        var result = await service.ValidateLdapSettingsAsync(request);

        Assert.False(result.IsValid);
        Assert.Equal(1, ldapService.ValidateSearchBasesCallCount);
        Assert.Equal(0, ldapService.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdapSettingsAsync_WithTestCredentials_WhenUserValidationFails_ReturnsUserValidationMessage()
    {
        await using var dbContext = CreateDbContext();
        var ldapService = new FakeLdapService
        {
            ValidateResult = new(false, "Directory user could not be found.")
        };
        var service = CreateService(dbContext, ldapService);

        var request = CreateValidateRequest(bindPassword: "plain-secret", testUserName: "john", testPassword: "pw");
        var result = await service.ValidateLdapSettingsAsync(request);

        Assert.False(result.IsValid);
        Assert.Equal("Directory user could not be found.", result.Message);
        Assert.Equal(1, ldapService.ValidateSearchBasesCallCount);
        Assert.Equal(1, ldapService.ValidateCallCount);
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
