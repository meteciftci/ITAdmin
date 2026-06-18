using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.Notifications;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class NotificationSender(
    AppDbContext context,
    ISecretProtector secretProtector,
    ISmsProviderRegistry smsProviderRegistry,
    IEmailProviderRegistry emailProviderRegistry,
    ILogger<NotificationSender> logger) : INotificationSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<SmsSendResult> SendSmsAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadSettingsAsync(NotificationChannels.Sms, NotificationProviderKeys.CustomHttp, cancellationToken);
        if (entity is null || !entity.IsEnabled)
        {
            return new SmsSendResult(false, "SMS provider is not configured or disabled.");
        }

        var runtime = BuildSmsRuntime(entity);
        var adapter = smsProviderRegistry.GetRequired(entity.ProviderKey);
        return await adapter.SendAsync(request, runtime, cancellationToken);
    }

    public async Task<EmailSendResult> SendEmailAsync(
        EmailSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadSettingsAsync(NotificationChannels.Email, NotificationProviderKeys.Smtp, cancellationToken);
        if (entity is null || !entity.IsEnabled)
        {
            return new EmailSendResult(false, "Email provider is not configured or disabled.");
        }

        var runtime = BuildEmailRuntime(entity);
        var adapter = emailProviderRegistry.GetRequired(entity.ProviderKey);
        return await adapter.SendAsync(request, runtime, cancellationToken);
    }

    private Task<Domain.Entities.NotificationProviderSettings?> LoadSettingsAsync(
        string channel,
        string providerKey,
        CancellationToken cancellationToken) =>
        context.NotificationProviderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Channel == channel && x.ProviderKey == providerKey,
                cancellationToken);

    private SmsProviderRuntimeSettings BuildSmsRuntime(Domain.Entities.NotificationProviderSettings entity)
    {
        var publicSettings = Deserialize<SmsCustomHttpPublicSettings>(entity.PublicSettingsJson)
            ?? new SmsCustomHttpPublicSettings();
        var secrets = DeserializeSecrets<SmsCustomHttpSecretSettings>(entity.EncryptedSecretSettingsJson)
            ?? new SmsCustomHttpSecretSettings();
        return new SmsProviderRuntimeSettings(publicSettings, secrets);
    }

    private EmailProviderRuntimeSettings BuildEmailRuntime(Domain.Entities.NotificationProviderSettings entity)
    {
        var publicSettings = Deserialize<EmailSmtpPublicSettings>(entity.PublicSettingsJson)
            ?? new EmailSmtpPublicSettings();
        var secrets = DeserializeSecrets<EmailSmtpSecretSettings>(entity.EncryptedSecretSettingsJson)
            ?? new EmailSmtpSecretSettings();
        return new EmailProviderRuntimeSettings(publicSettings, secrets);
    }

    private T? Deserialize<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<T>(json, JsonOptions);

    private T? DeserializeSecrets<T>(string? protectedJson) where T : class
    {
        if (string.IsNullOrWhiteSpace(protectedJson))
        {
            return null;
        }

        try
        {
            var json = secretProtector.Unprotect(protectedJson);
            return Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification provider protected settings could not be decrypted.");
            return null;
        }
    }
}
