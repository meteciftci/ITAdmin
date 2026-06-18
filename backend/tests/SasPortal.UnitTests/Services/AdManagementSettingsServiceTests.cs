using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.Fakes;

namespace SasPortal.UnitTests.Services;

public sealed class AdManagementSettingsServiceTests
{
    [Fact]
    public async Task GetSettingsAsync_WhenNoRecord_ReturnsDefaults()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.GetSettingsAsync();

        Assert.False(result.IsConfigured);
        Assert.False(result.IsEnabled);
        Assert.Equal(30, result.PowerShellTimeoutSeconds);
        Assert.False(result.HasServiceAccountPassword);
        Assert.Empty(result.PreferredDomainControllers);
        Assert.Null(result.LastValidatedAt);
    }

    [Fact]
    public async Task UpdateSettingsAsync_CreatesNewRecordAndEncryptsPassword()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "very-secret-pwd",
            preferredDomainControllers: new[] { "dc01.corp.local", "DC01.CORP.LOCAL", " dc02.corp.local " });

        var result = await service.UpdateSettingsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Settings);

        var stored = await dbContext.AdManagementSettings.SingleAsync();
        Assert.True(stored.IsEnabled);
        Assert.NotNull(stored.EncryptedServiceAccountPassword);
        Assert.StartsWith("protected:", stored.EncryptedServiceAccountPassword);
        Assert.Equal("protected:very-secret-pwd", stored.EncryptedServiceAccountPassword);

        Assert.NotNull(stored.PreferredDomainControllersJson);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(stored.PreferredDomainControllersJson!);
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed!.Count);

        Assert.True(result.Settings!.HasServiceAccountPassword);
        Assert.Equal(2, result.Settings.PreferredDomainControllers.Count);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ResponseDoesNotExposePassword()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "another-secret");

        var result = await service.UpdateSettingsAsync(request);

        Assert.True(result.IsSuccess);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Settings);
        Assert.DoesNotContain("another-secret", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithoutPassword_KeepsExistingEncryptedSecret()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateSettingsAsync(CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "initial-secret"));

        var beforeUpdate = await dbContext.AdManagementSettings.SingleAsync();
        var originalEncrypted = beforeUpdate.EncryptedServiceAccountPassword;
        dbContext.ChangeTracker.Clear();

        var update = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: null,
            domainFqdn: "corp.example.com");

        var result = await service.UpdateSettingsAsync(update);

        Assert.True(result.IsSuccess);

        var stored = await dbContext.AdManagementSettings.SingleAsync();
        Assert.Equal(originalEncrypted, stored.EncryptedServiceAccountPassword);
        Assert.Equal("corp.example.com", stored.DomainFqdn);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithClearFlag_RemovesPassword_WhenNotEnabled()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateSettingsAsync(CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "to-be-cleared"));

        dbContext.ChangeTracker.Clear();

        var update = CreateRequest(
            isEnabled: false,
            serviceAccountPassword: null,
            clearServiceAccountPassword: true);

        var result = await service.UpdateSettingsAsync(update);

        Assert.True(result.IsSuccess);

        var stored = await dbContext.AdManagementSettings.SingleAsync();
        Assert.Null(stored.EncryptedServiceAccountPassword);
        Assert.False(result.Settings!.HasServiceAccountPassword);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PreferredDomainControllers_IsTrimmedAndDeduped()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "x",
            preferredDomainControllers: new[]
            {
                " dc01.corp ",
                "DC01.CORP",
                "dc01.corp",
                "dc02.corp",
                string.Empty,
                "   "
            });

        var result = await service.UpdateSettingsAsync(request);
        Assert.True(result.IsSuccess);

        var dcs = result.Settings!.PreferredDomainControllers;
        Assert.Equal(2, dcs.Count);
        Assert.Equal("dc01.corp", dcs[0]);
        Assert.Equal("dc02.corp", dcs[1]);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenEnabled_RequiresMandatoryFields()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: true,
            domainFqdn: null,
            serviceAccountPassword: "secret");

        var result = await service.UpdateSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdManagementApiMessageKeys.Settings.MissingRequiredFields, result.MessageKey);
        Assert.Empty(dbContext.AdManagementSettings);
    }

    [Theory]
    [InlineData(null, "CORP", "DC=corp,DC=example,DC=com", "DC=corp,DC=example,DC=com", "OU=Users,DC=corp,DC=example,DC=com", "OU=Disabled,DC=corp,DC=example,DC=com")]
    [InlineData("corp.example.com", null, "DC=corp,DC=example,DC=com", "DC=corp,DC=example,DC=com", "OU=Users,DC=corp,DC=example,DC=com", "OU=Disabled,DC=corp,DC=example,DC=com")]
    [InlineData("corp.example.com", "CORP", null, "DC=corp,DC=example,DC=com", "OU=Users,DC=corp,DC=example,DC=com", "OU=Disabled,DC=corp,DC=example,DC=com")]
    [InlineData("corp.example.com", "CORP", "DC=corp,DC=example,DC=com", "DC=corp,DC=example,DC=com", "OU=Users,DC=corp,DC=example,DC=com", null)]
    public async Task UpdateSettingsAsync_WhenEnabled_RequiresAllMandatoryConnectionFields(
        string? domainFqdn,
        string? netbiosDomainName,
        string? defaultNamingContext,
        string? baseDn,
        string? usersRootOu,
        string? disabledUsersOu)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: true,
            domainFqdn: domainFqdn,
            netbiosDomainName: netbiosDomainName,
            defaultNamingContext: defaultNamingContext,
            baseDn: baseDn,
            usersRootOu: usersRootOu,
            disabledUsersOu: disabledUsersOu,
            serviceAccountPassword: "secret");

        var result = await service.UpdateSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdManagementApiMessageKeys.Settings.MissingRequiredFields, result.MessageKey);
        Assert.Empty(dbContext.AdManagementSettings);
        Assert.Equal(0, dbContext.AdOperationLogs.Count(x => x.OperationType == "SettingsValidated"));
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIsEnabledFalse_AllowsMissingConnectionFields()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: false,
            domainFqdn: null,
            netbiosDomainName: null,
            defaultNamingContext: null,
            baseDn: null,
            usersRootOu: null,
            disabledUsersOu: null,
            serviceAccountPassword: null);

        var result = await service.UpdateSettingsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Single(dbContext.AdManagementSettings);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenEnabled_PassesDefaultNamingContextToValidation()
    {
        await using var dbContext = CreateDbContext();
        var validator = new FakeAdManagementValidationService();
        var service = CreateService(dbContext, validator);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "secret",
            defaultNamingContext: "DC=corp,DC=example,DC=com",
            disabledUsersOu: "OU=Disabled,DC=corp,DC=example,DC=com");

        await service.UpdateSettingsAsync(request);

        Assert.NotNull(validator.LastConnection);
        Assert.Equal("DC=corp,DC=example,DC=com", validator.LastConnection!.DefaultNamingContext);
        Assert.Equal("OU=Disabled,DC=corp,DC=example,DC=com", validator.LastConnection.DisabledUsersOu);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WritesAuditAndOperationLog_WithoutPassword()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "topsecret-token");

        var result = await service.UpdateSettingsAsync(request);
        Assert.True(result.IsSuccess);

        var audit = Assert.Single(dbContext.AuditLogs.Where(x =>
            x.EntityName == "AdManagementSettings" && x.Action == "Update"));
        Assert.NotNull(audit.Description);
        Assert.DoesNotContain("topsecret-token", audit.Description!, StringComparison.Ordinal);

        var op = Assert.Single(dbContext.AdOperationLogs.Where(x => x.OperationType == "SettingsUpdated"));
        Assert.Equal("Succeeded", op.Status);
        Assert.NotNull(op.RequestSummaryJson);
        Assert.NotNull(op.AfterSnapshotJson);
        Assert.Null(op.ErrorCode);
        Assert.Null(op.ErrorMessage);
        Assert.DoesNotContain("topsecret-token", op.RequestSummaryJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("topsecret-token", op.BeforeSnapshotJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("topsecret-token", op.AfterSnapshotJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("encryptedServiceAccountPassword", op.AfterSnapshotJson!, StringComparison.OrdinalIgnoreCase);

        using var requestSummaryDocument = System.Text.Json.JsonDocument.Parse(op.RequestSummaryJson!);
        Assert.Equal("SettingsUpdated", requestSummaryDocument.RootElement.GetProperty("operation").GetString());

        using var afterSnapshotDocument = System.Text.Json.JsonDocument.Parse(op.AfterSnapshotJson!);
        Assert.True(afterSnapshotDocument.RootElement.GetProperty("hasServiceAccountPassword").GetBoolean());

        foreach (var entry in dbContext.AuditLogs)
        {
            Assert.NotNull(entry.Description);
            Assert.DoesNotContain("topsecret-token", entry.Description!, StringComparison.Ordinal);
        }

        foreach (var entry in dbContext.AdOperationLogs)
        {
            Assert.DoesNotContain("topsecret-token", entry.RequestSummaryJson ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("topsecret-token", entry.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task UpdateSettingsAsync_RejectsInvalidPowerShellTimeout()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: false,
            serviceAccountPassword: null,
            powerShellTimeoutSeconds: 1);

        var result = await service.UpdateSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Empty(dbContext.AdManagementSettings);
    }

    [Fact]
    public async Task RecordValidationResultAsync_WhenSucceeded_WritesAuditAndOperationLog_AndUpdatesLastValidation()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateSettingsAsync(CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "validation-secret-pwd"));

        dbContext.AuditLogs.RemoveRange(dbContext.AuditLogs);
        dbContext.AdOperationLogs.RemoveRange(dbContext.AdOperationLogs);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var checkedAt = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var result = new AdManagementValidationResult(
            IsValid: true,
            MessageKey: "AD yönetim ayarları doğrulandı.",
            CheckedAt: checkedAt,
            Details: new List<AdManagementValidationDetail>
            {
                new("serviceAccountBind", "Ok", null),
                new("baseDn", "Ok", null),
            });

        var validationRequest = CreateValidationRequest();

        await service.RecordValidationResultAsync(
            result,
            validationRequest,
            primaryDomainController: "dc01.corp.example.com");

        var settings = await dbContext.AdManagementSettings.SingleAsync();
        Assert.Equal("Ok", settings.LastValidationStatus);
        Assert.Equal(checkedAt.UtcDateTime, settings.LastValidatedAt);
        Assert.Equal("AD yönetim ayarları doğrulandı.", settings.LastValidationMessage);

        var audit = await dbContext.AuditLogs.SingleAsync(x =>
            x.EntityName == "AdManagementSettings" && x.Action == "Validate");
        Assert.Equal("AD management settings validation succeeded.", audit.Description);
        Assert.Equal(validationRequest.ActorUserId, audit.ActorUserId);
        Assert.Equal(validationRequest.ActorUserName, audit.ActorUserName);

        var op = await dbContext.AdOperationLogs.SingleAsync(x =>
            x.OperationType == "SettingsValidated");
        Assert.Equal("Succeeded", op.Status);
        Assert.Equal("AdManagementSettings", op.TargetObjectType);
        Assert.Equal("dc01.corp.example.com", op.DomainController);
        Assert.Null(op.ErrorMessage);
        Assert.NotNull(op.RequestSummaryJson);
        Assert.Contains("\"key\":\"serviceAccountBind\"", op.RequestSummaryJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordValidationResultAsync_WhenFailed_WritesFailureLogs_AndDoesNotLeakPassword()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateSettingsAsync(CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "super-secret-password"));

        dbContext.AuditLogs.RemoveRange(dbContext.AuditLogs);
        dbContext.AdOperationLogs.RemoveRange(dbContext.AdOperationLogs);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var checkedAt = new DateTimeOffset(2026, 5, 15, 12, 5, 0, TimeSpan.Zero);
        var failureMessage = "AD yönetim servis hesabı ile bağlantı kurulamadı.";
        var result = new AdManagementValidationResult(
            IsValid: false,
            MessageKey: failureMessage,
            CheckedAt: checkedAt,
            Details: new List<AdManagementValidationDetail>
            {
                new("serviceAccountBind", "Failed", failureMessage),
            });

        var validationRequest = CreateValidationRequest();

        await service.RecordValidationResultAsync(
            result,
            validationRequest,
            primaryDomainController: null);

        var settings = await dbContext.AdManagementSettings.SingleAsync();
        Assert.Equal("Failed", settings.LastValidationStatus);
        Assert.Equal(failureMessage, settings.LastValidationMessage);

        var audit = await dbContext.AuditLogs.SingleAsync(x =>
            x.EntityName == "AdManagementSettings" && x.Action == "Validate");
        Assert.NotNull(audit.Description);
        Assert.Contains("validation failed", audit.Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-password", audit.Description!, StringComparison.Ordinal);

        var op = await dbContext.AdOperationLogs.SingleAsync(x =>
            x.OperationType == "SettingsValidated");
        Assert.Equal("Failed", op.Status);
        Assert.Equal(AdOperationDiagnosticCodes.SettingsValidationFailed, op.ErrorCode);
        Assert.NotNull(op.ErrorMessage);
        using (var diagnosticDocument = System.Text.Json.JsonDocument.Parse(op.ErrorMessage!))
        {
            Assert.Equal(
                AdOperationDiagnosticCodes.SettingsValidationFailed,
                diagnosticDocument.RootElement.GetProperty("code").GetString());
            Assert.Equal("SettingsValidated", diagnosticDocument.RootElement.GetProperty("operation").GetString());
        }

        Assert.NotNull(op.RequestSummaryJson);
        using (var summaryDocument = System.Text.Json.JsonDocument.Parse(op.RequestSummaryJson!))
        {
            Assert.Equal("SettingsValidated", summaryDocument.RootElement.GetProperty("operation").GetString());
        }

        Assert.DoesNotContain("super-secret-password", op.RequestSummaryJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-password", op.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordValidationResultAsync_WhenNoSettingsRecord_StillWritesAuditAndOperationLog()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = new AdManagementValidationResult(
            IsValid: false,
            MessageKey: "AD yönetim ayarları eksik.",
            CheckedAt: DateTimeOffset.UtcNow,
            Details: new List<AdManagementValidationDetail>
            {
                new("serviceAccountBind", "Failed", "AD yönetim ayarları eksik."),
            });

        var validationRequest = CreateValidationRequest();

        await service.RecordValidationResultAsync(
            result,
            validationRequest,
            primaryDomainController: null);

        Assert.Single(dbContext.AuditLogs.Where(x =>
            x.EntityName == "AdManagementSettings" && x.Action == "Validate"));

        Assert.Single(dbContext.AdOperationLogs.Where(x => x.OperationType == "SettingsValidated"));
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIsEnabledFalse_DoesNotInvokeValidation()
    {
        await using var dbContext = CreateDbContext();
        var validator = new FakeAdManagementValidationService();
        var service = CreateService(dbContext, validator);

        var request = CreateRequest(isEnabled: false, serviceAccountPassword: null);

        var result = await service.UpdateSettingsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Validation);
        Assert.Equal(0, validator.InvocationCount);

        var stored = await dbContext.AdManagementSettings.SingleAsync();
        Assert.False(stored.IsEnabled);

        Assert.Empty(dbContext.AdOperationLogs.Where(x => x.OperationType == "SettingsValidated"));
        Assert.Single(dbContext.AdOperationLogs.Where(x => x.OperationType == "SettingsUpdated"));
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIsEnabledTrueAndValidationFails_DoesNotPersistSettings()
    {
        await using var dbContext = CreateDbContext();
        var validator = new FakeAdManagementValidationService
        {
            NextResult = new AdManagementValidationResult(
                IsValid: false,
                MessageKey: "AD yönetim servis hesabı ile bağlantı kurulamadı.",
                CheckedAt: DateTimeOffset.UtcNow,
                Details: new List<AdManagementValidationDetail>
                {
                    new("serviceAccountBind", "Failed", "AD yönetim servis hesabı ile bağlantı kurulamadı."),
                })
        };
        var service = CreateService(dbContext, validator);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "super-secret-password",
            preferredDomainControllers: new[] { "dc01.corp.local" });

        var result = await service.UpdateSettingsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Validation);
        Assert.False(result.Validation!.IsValid);
        Assert.Null(result.Settings);
        Assert.Equal(1, validator.InvocationCount);

        Assert.False(dbContext.AdManagementSettings.Any());

        var op = await dbContext.AdOperationLogs.SingleAsync(x => x.OperationType == "SettingsValidated");
        Assert.Equal("Failed", op.Status);
        Assert.Equal(AdOperationDiagnosticCodes.SettingsValidationFailed, op.ErrorCode);
        Assert.Equal("dc01.corp.local", op.DomainController);
        Assert.NotNull(op.RequestSummaryJson);
        Assert.NotNull(op.ErrorMessage);
        using (var diagnosticDocument = System.Text.Json.JsonDocument.Parse(op.ErrorMessage!))
        {
            Assert.Equal(
                AdOperationDiagnosticCodes.SettingsValidationFailed,
                diagnosticDocument.RootElement.GetProperty("code").GetString());
        }

        Assert.DoesNotContain("super-secret-password", op.RequestSummaryJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-password", op.ErrorMessage ?? string.Empty, StringComparison.Ordinal);

        Assert.Empty(dbContext.AdOperationLogs.Where(x => x.OperationType == "SettingsUpdated"));

        var audit = await dbContext.AuditLogs.SingleAsync(x =>
            x.EntityName == "AdManagementSettings" && x.Action == "Validate");
        Assert.Contains("validation failed", audit.Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-password", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIsEnabledTrueAndValidationSucceeds_PersistsAndLogsBothOperations()
    {
        await using var dbContext = CreateDbContext();
        var checkedAt = new DateTimeOffset(2026, 5, 15, 15, 0, 0, TimeSpan.Zero);
        var validator = new FakeAdManagementValidationService
        {
            NextResult = new AdManagementValidationResult(
                IsValid: true,
                MessageKey: "ok",
                CheckedAt: checkedAt,
                Details: new List<AdManagementValidationDetail>
                {
                    new("serviceAccountBind", "Ok", null),
                })
        };
        var service = CreateService(dbContext, validator);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "another-secret",
            preferredDomainControllers: new[] { "dc01.corp.local" });

        var result = await service.UpdateSettingsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Validation);
        Assert.True(result.Validation!.IsValid);
        Assert.NotNull(result.Settings);
        Assert.Equal(1, validator.InvocationCount);
        Assert.NotNull(validator.LastConnection);
        Assert.Equal("CORP", validator.LastConnection!.NetbiosDomainName);
        Assert.Equal("another-secret", validator.LastConnection!.ServiceAccountPassword);

        var stored = await dbContext.AdManagementSettings.SingleAsync();
        Assert.True(stored.IsEnabled);
        Assert.Equal("Ok", stored.LastValidationStatus);
        Assert.Equal(checkedAt.UtcDateTime, stored.LastValidatedAt);
        Assert.Equal("ok", stored.LastValidationMessage);

        Assert.Single(dbContext.AdOperationLogs.Where(x => x.OperationType == "SettingsValidated"));
        Assert.Single(dbContext.AdOperationLogs.Where(x => x.OperationType == "SettingsUpdated"));
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenValidationFails_ResponseDoesNotIncludePassword()
    {
        await using var dbContext = CreateDbContext();
        var failingMessage = "AD yönetim servis hesabı ile bağlantı kurulamadı.";
        var validator = new FakeAdManagementValidationService
        {
            NextResult = new AdManagementValidationResult(
                IsValid: false,
                MessageKey: failingMessage,
                CheckedAt: DateTimeOffset.UtcNow,
                Details: new List<AdManagementValidationDetail>
                {
                    new("serviceAccountBind", "Failed", failingMessage),
                })
        };
        var service = CreateService(dbContext, validator);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountPassword: "leak-check-password");

        var result = await service.UpdateSettingsAsync(request);

        Assert.False(result.IsSuccess);

        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("leak-check-password", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PassesNormalizedNetbiosToValidation()
    {
        await using var dbContext = CreateDbContext();
        var validator = new FakeAdManagementValidationService();
        var service = CreateService(dbContext, validator);

        var request = CreateRequest(
            isEnabled: true,
            serviceAccountUserName: "svc",
            serviceAccountPassword: "ok-secret");

        await service.UpdateSettingsAsync(request);

        Assert.NotNull(validator.LastConnection);
        Assert.Equal("CORP", validator.LastConnection!.NetbiosDomainName);
        Assert.Equal("svc", validator.LastConnection!.ServiceAccountUserName);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PersistsNormalizedDefaultUserCreationUpnSuffix()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest(
            isEnabled: false,
            defaultUserCreationUpnSuffix: "@Mugla.Bel.TR");

        var result = await service.UpdateSettingsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("mugla.bel.tr", result.Settings!.DefaultUserCreationUpnSuffix);

        var stored = await dbContext.AdManagementSettings.SingleAsync();
        Assert.Equal("mugla.bel.tr", stored.DefaultUserCreationUpnSuffix);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RejectsInvalidDefaultUserCreationUpnSuffix()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.UpdateSettingsAsync(
            CreateRequest(isEnabled: false, defaultUserCreationUpnSuffix: "not a valid suffix"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AdManagementApiMessageKeys.Settings.DefaultUpnSuffixInvalid, result.MessageKey);
        Assert.False(dbContext.AdManagementSettings.Any());
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultUserCreationUpnSuffix()
    {
        await using var dbContext = CreateDbContext();
        dbContext.AdManagementSettings.Add(new SasPortal.Domain.Entities.AdManagementSettings
        {
            IsEnabled = true,
            DomainFqdn = "corp.example.com",
            DefaultUserCreationUpnSuffix = "corp.example.com",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetSettingsAsync();

        Assert.Equal("corp.example.com", result.DefaultUserCreationUpnSuffix);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRecordExists_ReturnsIsConfiguredTrue()
    {
        await using var dbContext = CreateDbContext();
        dbContext.AdManagementSettings.Add(new SasPortal.Domain.Entities.AdManagementSettings
        {
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetSettingsAsync();

        Assert.True(result.IsConfigured);
        Assert.False(result.IsEnabled);
    }

    private static AdManagementSettingsService CreateService(
        AppDbContext context,
        FakeAdManagementValidationService? validationService = null) =>
        new(
            context,
            new FakeSecretProtector(),
            new AdOperationLogService(context),
            validationService ?? new FakeAdManagementValidationService(),
            NullLogger<AdManagementSettingsService>.Instance);

    private static AdManagementValidationRequest CreateValidationRequest() =>
        new(
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "validator",
            ActorIpAddress: "127.0.0.1",
            ActorUserAgent: "xunit");

    private static UpdateAdManagementSettingsRequest CreateRequest(
        bool isEnabled = false,
        string? domainFqdn = "corp.example.com",
        string? defaultUserCreationUpnSuffix = null,
        string? netbiosDomainName = "CORP",
        string? defaultNamingContext = "DC=corp,DC=example,DC=com",
        string? baseDn = "DC=corp,DC=example,DC=com",
        string? usersRootOu = "OU=Users,DC=corp,DC=example,DC=com",
        string? disabledUsersOu = "OU=Disabled,DC=corp,DC=example,DC=com",
        string? serviceAccountUserName = "svc_ad",
        string? serviceAccountPassword = null,
        bool clearServiceAccountPassword = false,
        IReadOnlyList<string>? preferredDomainControllers = null,
        int powerShellTimeoutSeconds = 30) =>
        new(
            IsEnabled: isEnabled,
            DomainFqdn: domainFqdn,
            DefaultUserCreationUpnSuffix: defaultUserCreationUpnSuffix,
            NetbiosDomainName: netbiosDomainName,
            DefaultNamingContext: defaultNamingContext,
            BaseDn: baseDn,
            UsersRootOu: usersRootOu,
            DisabledUsersOu: disabledUsersOu,
            GroupsSearchBase: null,
            ComputersSearchBase: null,
            PreferredDomainControllers: preferredDomainControllers,
            ServiceAccountUserName: serviceAccountUserName,
            ServiceAccountPassword: serviceAccountPassword,
            ClearServiceAccountPassword: clearServiceAccountPassword,
            PowerShellHealthEnabled: false,
            PowerShellTimeoutSeconds: powerShellTimeoutSeconds,
            NotificationSettings: AdManagementNotificationSettingsSerializer.CreateDefault(),
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "127.0.0.1",
            ActorUserAgent: "xunit");
}
