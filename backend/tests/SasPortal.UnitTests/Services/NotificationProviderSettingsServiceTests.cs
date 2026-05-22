using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models.Notifications;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.Fakes;

namespace SasPortal.UnitTests.Services;

public sealed class NotificationProviderSettingsServiceTests
{
    [Fact]
    public async Task UpdateSmsSettingsAsync_EncryptsSecrets_AndResponseDoesNotExposePassword()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateSmsUpdateRequest(basicPassword: "sms-secret");
        var result = await service.UpdateSmsSettingsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(result.SmsSettings!.HasBasicPassword);

        var stored = await dbContext.NotificationProviderSettings.SingleAsync();
        Assert.StartsWith("protected:", stored.EncryptedSecretSettingsJson, StringComparison.Ordinal);

        var serialized = System.Text.Json.JsonSerializer.Serialize(result.SmsSettings);
        Assert.DoesNotContain("sms-secret", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSmsSettingsAsync_WithoutPassword_KeepsExistingSecret()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateSmsSettingsAsync(CreateSmsUpdateRequest(basicPassword: "keep-me", authType: "Basic", basicUserName: "user"));
        var original = (await dbContext.NotificationProviderSettings.SingleAsync()).EncryptedSecretSettingsJson;
        dbContext.ChangeTracker.Clear();

        var result = await service.UpdateSmsSettingsAsync(
            CreateSmsUpdateRequest(basicPassword: null, authType: "Basic", basicUserName: "user", endpointUrl: "https://sms.example.com/send"));

        Assert.True(result.IsSuccess);
        var stored = await dbContext.NotificationProviderSettings.SingleAsync();
        Assert.Equal(original, stored.EncryptedSecretSettingsJson);
    }

    [Fact]
    public async Task UpdateSmsSettingsAsync_WritesSafeAuditDiff_ForPublicAndSecret()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.UpdateSmsSettingsAsync(
            CreateSmsUpdateRequest(
                isEnabled: false,
                method: "GET",
                endpointUrl: "https://old.example.com",
                authType: "None"));

        dbContext.ChangeTracker.Clear();

        await service.UpdateSmsSettingsAsync(
            CreateSmsUpdateRequest(
                isEnabled: true,
                method: "POST",
                endpointUrl: "https://new.example.com",
                authType: "BearerToken",
                bearerToken: "token-value"));

        var audit = await dbContext.AuditLogs
            .Where(x => x.EntityName == "NotificationProviderSettings" && x.Description!.Contains("Method GET -> POST"))
            .SingleAsync();
        Assert.Contains("IsEnabled False -> True", audit.Description!, StringComparison.Ordinal);
        Assert.Contains("Method GET -> POST", audit.Description!, StringComparison.Ordinal);
        Assert.Contains("AuthType None -> BearerToken", audit.Description!, StringComparison.Ordinal);
        Assert.Contains("Secret.BearerToken changed", audit.Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", audit.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestSmsAsync_WritesMaskedRecipientAudit()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, smsResult: new SmsSendResult(true, "ok"));

        await service.UpdateSmsSettingsAsync(CreateSmsUpdateRequest());
        dbContext.ChangeTracker.Clear();

        await service.TestSmsAsync(
            new TestSmsProviderRequest(
                "+905551234567",
                "hello",
                Guid.NewGuid(),
                "tester",
                "127.0.0.1",
                "xunit"));

        var audit = await dbContext.AuditLogs
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(x => x.Description!.Contains("Notification test sent", StringComparison.Ordinal));

        Assert.Contains("Recipient:", audit.Description!);
        Assert.Contains("*", audit.Description!);
        Assert.DoesNotContain("hello", audit.Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("551234567", audit.Description!, StringComparison.Ordinal);
    }

    private static NotificationProviderSettingsService CreateService(
        AppDbContext context,
        SmsSendResult? smsResult = null,
        EmailSendResult? emailResult = null) =>
        new(
            context,
            new FakeSecretProtector(),
            new FakeSmsProviderRegistry(smsResult ?? new SmsSendResult(true, "valid")),
            new FakeEmailProviderRegistry(emailResult ?? new EmailSendResult(true, "valid")));

    private static UpdateSmsProviderSettingsRequest CreateSmsUpdateRequest(
        bool isEnabled = true,
        string endpointUrl = "https://sms.example.com/send",
        string method = "POST",
        string authType = "None",
        string? basicUserName = null,
        string? basicPassword = null,
        string? bearerToken = null) =>
        new(
            isEnabled,
            "SMS",
            "SENDER",
            30,
            endpointUrl,
            method,
            "application/json",
            authType,
            null,
            basicUserName,
            basicPassword,
            bearerToken,
            null,
            [],
            [],
            "{\"phone\":\"{{phone}}\",\"message\":\"{{message}}\"}",
            [200],
            null,
            "Preserve",
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

    private sealed class FakeSmsProviderRegistry(SmsSendResult result) : ISmsProviderRegistry
    {
        public IReadOnlyList<ISmsProviderAdapter> GetProviders() => [new FakeSmsAdapter(result)];

        public ISmsProviderAdapter GetRequired(string providerKey) => new FakeSmsAdapter(result);
    }

    private sealed class FakeSmsAdapter(SmsSendResult result) : ISmsProviderAdapter
    {
        public string ProviderKey => NotificationProviderKeys.CustomHttp;
        public string DisplayName => "Custom HTTP";
        public SmsProviderDefinition GetDefinition() => new(ProviderKey, DisplayName);
        public Task<SmsSendResult> SendAsync(SmsSendRequest request, SmsProviderRuntimeSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
        public Task<SmsSendResult> ValidateAsync(SmsProviderRuntimeSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeEmailProviderRegistry(EmailSendResult result) : IEmailProviderRegistry
    {
        public IReadOnlyList<IEmailProviderAdapter> GetProviders() => [new FakeEmailAdapter(result)];
        public IEmailProviderAdapter GetRequired(string providerKey) => new FakeEmailAdapter(result);
    }

    private sealed class FakeEmailAdapter(EmailSendResult result) : IEmailProviderAdapter
    {
        public string ProviderKey => NotificationProviderKeys.Smtp;
        public string DisplayName => "SMTP";
        public EmailProviderDefinition GetDefinition() => new(ProviderKey, DisplayName);
        public Task<EmailSendResult> SendAsync(EmailSendRequest request, EmailProviderRuntimeSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
        public Task<EmailSendResult> ValidateAsync(EmailProviderRuntimeSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
