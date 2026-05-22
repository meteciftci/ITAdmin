using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Audit;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models.Notifications;
using SasPortal.Application.Common.Notifications;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class NotificationProviderSettingsService(
    AppDbContext context,
    ISecretProtector secretProtector,
    ISmsProviderRegistry smsProviderRegistry,
    IEmailProviderRegistry emailProviderRegistry) : INotificationProviderSettingsService
{
    private const int AuditIpAddressMaxLength = 64;
    private const int AuditUserAgentMaxLength = 1024;

    public async Task<SmsProviderSettingsResponse> GetSmsSettingsAsync(CancellationToken cancellationToken = default)
    {
        var entity = await LoadAsync(NotificationChannels.Sms, NotificationProviderKeys.CustomHttp, cancellationToken)
            ?? CreateDefaultEntity(NotificationChannels.Sms, NotificationProviderKeys.CustomHttp);
        return MapSmsResponse(entity);
    }

    public async Task<EmailProviderSettingsResponse> GetEmailSettingsAsync(CancellationToken cancellationToken = default)
    {
        var entity = await LoadAsync(NotificationChannels.Email, NotificationProviderKeys.Smtp, cancellationToken)
            ?? CreateDefaultEntity(NotificationChannels.Email, NotificationProviderKeys.Smtp);
        return MapEmailResponse(entity);
    }

    public async Task<NotificationProviderOperationResult> UpdateSmsSettingsAsync(
        UpdateSmsProviderSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateTrackedAsync(
            NotificationChannels.Sms,
            NotificationProviderKeys.CustomHttp,
            cancellationToken);

        var beforePublic = DeserializeSmsPublic(entity);
        var beforeSecrets = DeserializeSmsSecrets(entity);

        var mergedSecrets = MergeSmsSecrets(beforeSecrets, request);
        var afterPublic = BuildSmsPublicFromRequest(request);

        var runtime = new SmsProviderRuntimeSettings(afterPublic, mergedSecrets);
        var smsAdapter = smsProviderRegistry.GetRequired(NotificationProviderKeys.CustomHttp);
        var smsValidation = await smsAdapter.ValidateAsync(runtime, cancellationToken);
        if (!smsValidation.IsSuccess)
        {
            return new NotificationProviderOperationResult(false, smsValidation.Message);
        }

        var auditChanges = BuildSmsAuditChanges(beforePublic, afterPublic, beforeSecrets, mergedSecrets, entity.IsEnabled, request.IsEnabled);

        entity.IsEnabled = request.IsEnabled;
        entity.DisplayName = request.DisplayName?.Trim();
        entity.PublicSettingsJson = NotificationProviderSettingsJson.SerializePublic(afterPublic);
        entity.EncryptedSecretSettingsJson = NotificationProviderSettingsJson.ProtectSecrets(mergedSecrets, secretProtector);

        await ApplyUpdateMetadataAsync(entity, request.ActorUserName, cancellationToken);
        await WriteUpdateAuditAsync(
            entity,
            "Notification provider settings updated. Channel: Sms. Provider: CustomHttp.",
            auditChanges,
            request,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        return new NotificationProviderOperationResult(
            true,
            "SMS provider settings saved.",
            MapSmsResponse(entity));
    }

    public async Task<NotificationProviderOperationResult> UpdateEmailSettingsAsync(
        UpdateEmailProviderSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateTrackedAsync(
            NotificationChannels.Email,
            NotificationProviderKeys.Smtp,
            cancellationToken);

        var beforePublic = DeserializeEmailPublic(entity);
        var beforeSecrets = DeserializeEmailSecrets(entity);

        var mergedSecrets = MergeEmailSecrets(beforeSecrets, request.Password);
        var afterPublic = BuildEmailPublicFromRequest(request);

        var runtime = new EmailProviderRuntimeSettings(afterPublic, mergedSecrets);
        var emailAdapter = emailProviderRegistry.GetRequired(NotificationProviderKeys.Smtp);
        var emailValidation = await emailAdapter.ValidateAsync(runtime, cancellationToken);
        if (!emailValidation.IsSuccess)
        {
            return new NotificationProviderOperationResult(false, emailValidation.Message);
        }

        var auditChanges = BuildEmailAuditChanges(
            beforePublic,
            afterPublic,
            beforeSecrets,
            mergedSecrets,
            entity.IsEnabled,
            request.IsEnabled);

        entity.IsEnabled = request.IsEnabled;
        entity.DisplayName = request.DisplayName?.Trim();
        entity.PublicSettingsJson = NotificationProviderSettingsJson.SerializePublic(afterPublic);
        entity.EncryptedSecretSettingsJson = NotificationProviderSettingsJson.ProtectSecrets(mergedSecrets, secretProtector);

        await ApplyUpdateMetadataAsync(entity, request.ActorUserName, cancellationToken);
        await WriteUpdateAuditAsync(
            entity,
            "Notification provider settings updated. Channel: Email. Provider: Smtp.",
            auditChanges,
            request,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        return new NotificationProviderOperationResult(
            true,
            "Email provider settings saved.",
            EmailSettings: MapEmailResponse(entity));
    }

    public async Task<NotificationProviderOperationResult> TestSmsAsync(
        TestSmsProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.NotificationProviderSettings
            .FirstOrDefaultAsync(
                x => x.Channel == NotificationChannels.Sms && x.ProviderKey == NotificationProviderKeys.CustomHttp,
                cancellationToken);

        if (entity is null || string.IsNullOrWhiteSpace(entity.PublicSettingsJson))
        {
            return new NotificationProviderOperationResult(false, "SMS provider settings must be saved before testing.");
        }

        var runtime = new SmsProviderRuntimeSettings(
            DeserializeSmsPublic(entity),
            DeserializeSmsSecrets(entity));

        var adapter = smsProviderRegistry.GetRequired(entity.ProviderKey);
        var result = await adapter.SendAsync(
            new SmsSendRequest(request.PhoneNumber, request.Message),
            runtime,
            cancellationToken);

        await ApplyValidationResultAsync(entity, result.IsSuccess, result.Message, request.ActorUserName, cancellationToken);
        await WriteTestAuditAsync(
            NotificationChannels.Sms,
            NotificationProviderKeys.CustomHttp,
            "CustomHttp",
            NotificationRecipientMasker.MaskPhone(request.PhoneNumber),
            result.IsSuccess,
            result.IsSuccess ? "Success" : result.Message,
            request,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new NotificationProviderOperationResult(
            result.IsSuccess,
            result.Message,
            ProviderSummary: result.ProviderSummary);
    }

    public async Task<NotificationProviderOperationResult> TestEmailAsync(
        TestEmailProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.NotificationProviderSettings
            .FirstOrDefaultAsync(
                x => x.Channel == NotificationChannels.Email && x.ProviderKey == NotificationProviderKeys.Smtp,
                cancellationToken);

        if (entity is null || string.IsNullOrWhiteSpace(entity.PublicSettingsJson))
        {
            return new NotificationProviderOperationResult(false, "Email provider settings must be saved before testing.");
        }

        var runtime = new EmailProviderRuntimeSettings(
            DeserializeEmailPublic(entity),
            DeserializeEmailSecrets(entity));

        var adapter = emailProviderRegistry.GetRequired(entity.ProviderKey);
        var result = await adapter.SendAsync(
            new EmailSendRequest(request.RecipientEmail, request.Subject, request.Body),
            runtime,
            cancellationToken);

        await ApplyValidationResultAsync(entity, result.IsSuccess, result.Message, request.ActorUserName, cancellationToken);
        await WriteTestAuditAsync(
            NotificationChannels.Email,
            NotificationProviderKeys.Smtp,
            "Smtp",
            NotificationRecipientMasker.MaskEmail(request.RecipientEmail),
            result.IsSuccess,
            result.IsSuccess ? "Success" : result.Message,
            request,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new NotificationProviderOperationResult(
            result.IsSuccess,
            result.Message,
            EmailSettings: MapEmailResponse(entity),
            ProviderSummary: result.ProviderSummary);
    }

    private async Task<NotificationProviderSettings?> LoadAsync(
        string channel,
        string providerKey,
        CancellationToken cancellationToken) =>
        await context.NotificationProviderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Channel == channel && x.ProviderKey == providerKey, cancellationToken);

    private static NotificationProviderSettings CreateDefaultEntity(string channel, string providerKey) =>
        new()
        {
            Channel = channel,
            ProviderKey = providerKey,
            IsEnabled = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private async Task<NotificationProviderSettings> GetOrCreateTrackedAsync(
        string channel,
        string providerKey,
        CancellationToken cancellationToken)
    {
        var entity = await context.NotificationProviderSettings
            .FirstOrDefaultAsync(x => x.Channel == channel && x.ProviderKey == providerKey, cancellationToken);

        if (entity is not null)
        {
            return entity;
        }

        var now = DateTimeOffset.UtcNow;
        entity = new NotificationProviderSettings
        {
            Channel = channel,
            ProviderKey = providerKey,
            IsEnabled = false,
            CreatedAt = now,
        };

        await context.NotificationProviderSettings.AddAsync(entity, cancellationToken);
        return entity;
    }

    private SmsCustomHttpPublicSettings DeserializeSmsPublic(NotificationProviderSettings entity) =>
        NotificationProviderSettingsJson.DeserializePublic<SmsCustomHttpPublicSettings>(entity.PublicSettingsJson)
        ?? new SmsCustomHttpPublicSettings();

    private SmsCustomHttpSecretSettings DeserializeSmsSecrets(NotificationProviderSettings entity) =>
        NotificationProviderSettingsJson.UnprotectSecrets<SmsCustomHttpSecretSettings>(
            entity.EncryptedSecretSettingsJson,
            secretProtector)
        ?? new SmsCustomHttpSecretSettings();

    private EmailSmtpPublicSettings DeserializeEmailPublic(NotificationProviderSettings entity) =>
        NotificationProviderSettingsJson.DeserializePublic<EmailSmtpPublicSettings>(entity.PublicSettingsJson)
        ?? new EmailSmtpPublicSettings();

    private EmailSmtpSecretSettings DeserializeEmailSecrets(NotificationProviderSettings entity) =>
        NotificationProviderSettingsJson.UnprotectSecrets<EmailSmtpSecretSettings>(
            entity.EncryptedSecretSettingsJson,
            secretProtector)
        ?? new EmailSmtpSecretSettings();

    private static SmsCustomHttpPublicSettings BuildSmsPublicFromRequest(UpdateSmsProviderSettingsRequest request) =>
        new()
        {
            IsEnabled = request.IsEnabled,
            DisplayName = request.DisplayName?.Trim(),
            Sender = request.Sender?.Trim(),
            TimeoutSeconds = request.TimeoutSeconds,
            EndpointUrl = request.EndpointUrl.Trim(),
            Method = request.Method.Trim(),
            ContentType = request.ContentType.Trim(),
            AuthType = request.AuthType.Trim(),
            ApiKeyName = request.ApiKeyName?.Trim(),
            Headers = NormalizePairs(request.Headers),
            QueryParameters = NormalizePairs(request.QueryParameters),
            BodyTemplate = request.BodyTemplate,
            SuccessStatusCodes = request.SuccessStatusCodes.Count == 0 ? [200] : request.SuccessStatusCodes.ToList(),
            SuccessBodyContains = request.SuccessBodyContains?.Trim(),
            TurkishCharacterMode = request.TurkishCharacterMode.Trim(),
        };

    private static EmailSmtpPublicSettings BuildEmailPublicFromRequest(UpdateEmailProviderSettingsRequest request) =>
        new()
        {
            IsEnabled = request.IsEnabled,
            DisplayName = request.DisplayName?.Trim(),
            Host = request.Host.Trim(),
            Port = request.Port,
            UseSsl = request.UseSsl,
            UserName = request.UserName?.Trim(),
            FromAddress = request.FromAddress.Trim(),
            FromDisplayName = request.FromDisplayName?.Trim(),
            TimeoutSeconds = request.TimeoutSeconds,
        };

    private static SmsCustomHttpSecretSettings MergeSmsSecrets(
        SmsCustomHttpSecretSettings existing,
        UpdateSmsProviderSettingsRequest request) =>
        new()
        {
            BasicUserName = CoalesceSecret(request.BasicUserName, existing.BasicUserName),
            BasicPassword = CoalesceSecret(request.BasicPassword, existing.BasicPassword),
            BearerToken = CoalesceSecret(request.BearerToken, existing.BearerToken),
            ApiKeyValue = CoalesceSecret(request.ApiKeyValue, existing.ApiKeyValue),
        };

    private static EmailSmtpSecretSettings MergeEmailSecrets(
        EmailSmtpSecretSettings existing,
        string? password) =>
        new()
        {
            Password = CoalesceSecret(password, existing.Password),
        };

    private static string? CoalesceSecret(string? incoming, string? existing) =>
        string.IsNullOrWhiteSpace(incoming) ? existing : incoming.Trim();

    private static IReadOnlyList<NotificationKeyValuePair> NormalizePairs(
        IReadOnlyList<NotificationKeyValuePair> pairs) =>
        pairs
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => new NotificationKeyValuePair(x.Key.Trim(), x.Value ?? string.Empty))
            .ToList();

    private SmsProviderSettingsResponse MapSmsResponse(NotificationProviderSettings entity)
    {
        var publicSettings = DeserializeSmsPublic(entity);
        var secrets = DeserializeSmsSecrets(entity);
        return new SmsProviderSettingsResponse(
            entity.Channel,
            entity.ProviderKey,
            entity.IsEnabled,
            entity.DisplayName ?? publicSettings.DisplayName,
            publicSettings.Sender,
            publicSettings.TimeoutSeconds,
            publicSettings.EndpointUrl,
            publicSettings.Method,
            publicSettings.ContentType,
            publicSettings.AuthType,
            publicSettings.ApiKeyName,
            publicSettings.Headers,
            publicSettings.QueryParameters,
            publicSettings.BodyTemplate,
            publicSettings.SuccessStatusCodes,
            publicSettings.SuccessBodyContains,
            publicSettings.TurkishCharacterMode,
            !string.IsNullOrWhiteSpace(secrets.BasicPassword),
            !string.IsNullOrWhiteSpace(secrets.BearerToken),
            !string.IsNullOrWhiteSpace(secrets.ApiKeyValue),
            entity.LastValidatedAt,
            entity.LastValidationStatus,
            entity.LastValidationMessage);
    }

    private EmailProviderSettingsResponse MapEmailResponse(NotificationProviderSettings entity)
    {
        var publicSettings = DeserializeEmailPublic(entity);
        var secrets = DeserializeEmailSecrets(entity);
        return new EmailProviderSettingsResponse(
            entity.Channel,
            entity.ProviderKey,
            entity.IsEnabled,
            entity.DisplayName ?? publicSettings.DisplayName,
            publicSettings.Host,
            publicSettings.Port,
            publicSettings.UseSsl,
            publicSettings.UserName,
            publicSettings.FromAddress,
            publicSettings.FromDisplayName,
            publicSettings.TimeoutSeconds,
            !string.IsNullOrWhiteSpace(secrets.Password),
            entity.LastValidatedAt,
            entity.LastValidationStatus,
            entity.LastValidationMessage);
    }

    private static List<AuditFieldChange> BuildSmsAuditChanges(
        SmsCustomHttpPublicSettings beforePublic,
        SmsCustomHttpPublicSettings afterPublic,
        SmsCustomHttpSecretSettings beforeSecrets,
        SmsCustomHttpSecretSettings afterSecrets,
        bool beforeEnabled,
        bool afterEnabled)
    {
        var changes = new List<AuditFieldChange>
        {
            AuditChangeSummaryBuilder.PublicField("IsEnabled", beforeEnabled.ToString(), afterEnabled.ToString()),
            AuditChangeSummaryBuilder.PublicField("EndpointUrl", beforePublic.EndpointUrl, afterPublic.EndpointUrl),
            AuditChangeSummaryBuilder.PublicField("Method", beforePublic.Method, afterPublic.Method),
            AuditChangeSummaryBuilder.PublicField("ContentType", beforePublic.ContentType, afterPublic.ContentType),
            AuditChangeSummaryBuilder.PublicField("AuthType", beforePublic.AuthType, afterPublic.AuthType),
            AuditChangeSummaryBuilder.PublicField("Sender", beforePublic.Sender, afterPublic.Sender),
            AuditChangeSummaryBuilder.PublicField("TimeoutSeconds", beforePublic.TimeoutSeconds.ToString(), afterPublic.TimeoutSeconds.ToString()),
            AuditChangeSummaryBuilder.PublicField("TurkishCharacterMode", beforePublic.TurkishCharacterMode, afterPublic.TurkishCharacterMode, treatAsLongText: false),
            AuditChangeSummaryBuilder.PublicField("BodyTemplate", beforePublic.BodyTemplate, afterPublic.BodyTemplate, treatAsLongText: true),
        };

        AppendSecretChange(changes, "Secret.BasicPassword", beforeSecrets.BasicPassword, afterSecrets.BasicPassword);
        AppendSecretChange(changes, "Secret.BearerToken", beforeSecrets.BearerToken, afterSecrets.BearerToken);
        AppendSecretChange(changes, "Secret.ApiKeyValue", beforeSecrets.ApiKeyValue, afterSecrets.ApiKeyValue);

        return changes
            .Where(IsMeaningfulChange)
            .ToList();
    }

    private static List<AuditFieldChange> BuildEmailAuditChanges(
        EmailSmtpPublicSettings beforePublic,
        EmailSmtpPublicSettings afterPublic,
        EmailSmtpSecretSettings beforeSecrets,
        EmailSmtpSecretSettings afterSecrets,
        bool beforeEnabled,
        bool afterEnabled)
    {
        var changes = new List<AuditFieldChange>
        {
            AuditChangeSummaryBuilder.PublicField("IsEnabled", beforeEnabled.ToString(), afterEnabled.ToString()),
            AuditChangeSummaryBuilder.PublicField("Host", beforePublic.Host, afterPublic.Host),
            AuditChangeSummaryBuilder.PublicField("Port", beforePublic.Port.ToString(), afterPublic.Port.ToString()),
            AuditChangeSummaryBuilder.PublicField("UseSsl", beforePublic.UseSsl.ToString(), afterPublic.UseSsl.ToString()),
            AuditChangeSummaryBuilder.PublicField("UserName", beforePublic.UserName, afterPublic.UserName),
            AuditChangeSummaryBuilder.PublicField("FromAddress", beforePublic.FromAddress, afterPublic.FromAddress),
            AuditChangeSummaryBuilder.PublicField("TimeoutSeconds", beforePublic.TimeoutSeconds.ToString(), afterPublic.TimeoutSeconds.ToString()),
        };

        AppendSecretChange(changes, "Password", beforeSecrets.Password, afterSecrets.Password);

        return changes
            .Where(IsMeaningfulChange)
            .ToList();
    }

    private static void AppendSecretChange(
        ICollection<AuditFieldChange> changes,
        string fieldName,
        string? before,
        string? after)
    {
        var hadBefore = !string.IsNullOrWhiteSpace(before);
        var hasAfter = !string.IsNullOrWhiteSpace(after);
        if (hadBefore == hasAfter && string.Equals(before?.Trim(), after?.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(AuditChangeSummaryBuilder.SensitiveChanged(fieldName, hadBefore, hasAfter));
    }

    private static bool IsMeaningfulChange(AuditFieldChange change)
    {
        if (change.IsSensitive)
        {
            return true;
        }

        if (change.DisplayMode == AuditChangeDisplayMode.ChangedOnly
            || change.DisplayMode == AuditChangeDisplayMode.Cleared)
        {
            return true;
        }

        return !string.Equals(change.OldValue, change.NewValue, StringComparison.Ordinal);
    }

    private static Task ApplyUpdateMetadataAsync(
        NotificationProviderSettings entity,
        string? actor,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (entity.CreatedBy is null)
        {
            entity.CreatedBy = actor;
            entity.CreatedAt = now;
        }

        entity.UpdatedAt = now;
        entity.UpdatedBy = actor;
        return Task.CompletedTask;
    }

    private async Task ApplyValidationResultAsync(
        NotificationProviderSettings entity,
        bool isSuccess,
        string message,
        string? actor,
        CancellationToken cancellationToken)
    {
        entity.LastValidatedAt = DateTimeOffset.UtcNow;
        entity.LastValidationStatus = isSuccess ? "Ok" : "Failed";
        entity.LastValidationMessage = message;
        entity.UpdatedAt = entity.LastValidatedAt;
        entity.UpdatedBy = actor;
        context.NotificationProviderSettings.Update(entity);
    }

    private async Task WriteUpdateAuditAsync(
        NotificationProviderSettings entity,
        string prefix,
        IReadOnlyList<AuditFieldChange> changes,
        UpdateSmsProviderSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var description = AuditChangeSummaryBuilder.BuildUpdateDescription(prefix, changes);
        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "NotificationProviderSettings",
                EntityId = $"{entity.Channel}/{entity.ProviderKey}",
                Description = description,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private async Task WriteUpdateAuditAsync(
        NotificationProviderSettings entity,
        string prefix,
        IReadOnlyList<AuditFieldChange> changes,
        UpdateEmailProviderSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var description = AuditChangeSummaryBuilder.BuildUpdateDescription(prefix, changes);
        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "NotificationProviderSettings",
                EntityId = $"{entity.Channel}/{entity.ProviderKey}",
                Description = description,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private async Task WriteTestAuditAsync(
        string channel,
        string providerKey,
        string providerName,
        string maskedRecipient,
        bool isSuccess,
        string resultSummary,
        TestSmsProviderRequest request,
        CancellationToken cancellationToken)
    {
        var description = isSuccess
            ? $"Notification test sent. Channel: {channel}. Provider: {providerName}. Recipient: {maskedRecipient}. Result: Success."
            : $"Notification test failed. Channel: {channel}. Provider: {providerName}. Recipient: {maskedRecipient}. Error: {resultSummary}.";

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "NotificationProviderSettings",
                EntityId = $"{channel}/{providerKey}",
                Description = TruncateDescription(description),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private async Task WriteTestAuditAsync(
        string channel,
        string providerKey,
        string providerName,
        string maskedRecipient,
        bool isSuccess,
        string resultSummary,
        TestEmailProviderRequest request,
        CancellationToken cancellationToken)
    {
        var description = isSuccess
            ? $"Notification test sent. Channel: {channel}. Provider: {providerName}. Recipient: {maskedRecipient}. Result: Success."
            : $"Notification test failed. Channel: {channel}. Provider: {providerName}. Recipient: {maskedRecipient}. Error: {resultSummary}.";

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "NotificationProviderSettings",
                EntityId = $"{channel}/{providerKey}",
                Description = TruncateDescription(description),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private static string TruncateDescription(string description) =>
        description.Length <= AuditChangeSummaryBuilder.DefaultMaxLength
            ? description
            : $"{description[..(AuditChangeSummaryBuilder.DefaultMaxLength - 3)]}...";

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
