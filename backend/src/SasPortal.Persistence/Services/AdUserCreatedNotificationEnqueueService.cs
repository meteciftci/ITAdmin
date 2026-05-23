using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Application.Common.Models.Notifications;
using SasPortal.Application.Common.Notifications;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed partial class AdUserCreatedNotificationEnqueueService(
    AppDbContext context,
    INotificationOutboxService outboxService,
    INotificationTemplateRenderer templateRenderer) : IAdUserCreatedNotificationEnqueueService
{
    private const string DefaultApplicationName = "SAS-Portal";

    public async Task<AdUserCreatedNotificationSummary> EnqueueUserCreatedAsync(
        AdUserCreatedNotificationEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<string>();
        var queuedCount = 0;
        var skippedCount = 0;

        try
        {
            var settingsEntity = await context.AdManagementSettings
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var notificationSettings = AdManagementNotificationSettingsSerializer.Deserialize(
                settingsEntity?.NotificationSettingsJson);
            var userCreatedSettings = notificationSettings.UserCreated;

            if (!userCreatedSettings.IsEnabled)
            {
                return new AdUserCreatedNotificationSummary(0, 0, messages);
            }

            if (userCreatedSettings.SmsEnabled)
            {
                var smsResult = await TryEnqueueChannelAsync(
                    NotificationChannels.Sms,
                    userCreatedSettings.SmsRecipientSource,
                    request,
                    cancellationToken);
                queuedCount += smsResult.QueuedCount;
                skippedCount += smsResult.SkippedCount;
                messages.AddRange(smsResult.Messages);
            }

            if (userCreatedSettings.EmailEnabled)
            {
                var emailResult = await TryEnqueueChannelAsync(
                    NotificationChannels.Email,
                    userCreatedSettings.EmailRecipientSource,
                    request,
                    cancellationToken);
                queuedCount += emailResult.QueuedCount;
                skippedCount += emailResult.SkippedCount;
                messages.AddRange(emailResult.Messages);
            }
        }
        catch (Exception)
        {
            messages.Add("Notification enqueue failed.");
            skippedCount += 1;
        }

        return new AdUserCreatedNotificationSummary(queuedCount, skippedCount, messages);
    }

    private async Task<ChannelEnqueueResult> TryEnqueueChannelAsync(
        string channel,
        AdManagementNotificationRecipientSource? recipientSource,
        AdUserCreatedNotificationEnqueueRequest request,
        CancellationToken cancellationToken)
    {
        var messages = new List<string>();

        var template = await FindActiveTemplateAsync(channel, cancellationToken);
        if (template is null)
        {
            messages.Add(
                $"Active notification template was not found for {NotificationModuleKeys.AdManagement}/{NotificationEventKeys.UserCreated}/{channel}.");
            return new ChannelEnqueueResult(0, 1, messages);
        }

        var recipient = ResolveRecipient(recipientSource, channel, request);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            messages.Add("Notification recipient could not be resolved.");
            return new ChannelEnqueueResult(0, 1, messages);
        }

        if (!IsRecipientValid(channel, recipient))
        {
            messages.Add("Notification recipient format is invalid.");
            return new ChannelEnqueueResult(0, 1, messages);
        }

        var variables = BuildTemplateVariables(request);
        var body = templateRenderer.Render(template.BodyTemplate, variables);
        var subject = string.IsNullOrWhiteSpace(template.SubjectTemplate)
            ? null
            : templateRenderer.Render(template.SubjectTemplate, variables);

        if (string.IsNullOrWhiteSpace(body))
        {
            messages.Add("Notification body could not be rendered.");
            return new ChannelEnqueueResult(0, 1, messages);
        }

        var enqueueResult = await outboxService.EnqueueAsync(
            new NotificationOutboxEnqueueRequest(
                channel,
                ProviderKey: null,
                recipient,
                subject,
                body,
                NotificationModuleKeys.AdManagement,
                NotificationEventKeys.UserCreated,
                RelatedEntityType: "AdUser",
                RelatedEntityId: request.CreatedUser.Id,
                CorrelationId: null,
                Priority: 0,
                MaxAttempts: null,
                CreatedBy: request.ActorUserName),
            cancellationToken);

        if (!enqueueResult.IsSuccess)
        {
            messages.Add(enqueueResult.Message);
            return new ChannelEnqueueResult(0, 1, messages);
        }

        messages.Add(
            $"Notification queued for {channel}. Recipient: {MaskRecipientForLog(channel, recipient)}.");
        return new ChannelEnqueueResult(1, 0, messages);
    }

    private async Task<Domain.Entities.NotificationTemplate?> FindActiveTemplateAsync(
        string channel,
        CancellationToken cancellationToken)
    {
        var templates = await context.NotificationTemplates
            .AsNoTracking()
            .Where(x =>
                x.ModuleKey == NotificationModuleKeys.AdManagement
                && x.EventKey == NotificationEventKeys.UserCreated
                && x.Channel == channel
                && x.IsEnabled)
            .ToListAsync(cancellationToken);

        if (templates.Count == 0)
        {
            return null;
        }

        if (templates.Count > 1)
        {
            return templates[0];
        }

        return templates[0];
    }

    private static IReadOnlyDictionary<string, object?> BuildTemplateVariables(
        AdUserCreatedNotificationEnqueueRequest request) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["displayName"] = request.CreatedUser.DisplayName,
            ["username"] = request.CreatedUser.SamAccountName,
            ["upn"] = request.CreatedUser.UserPrincipalName,
            ["department"] = request.CreateRequest.Department?.Trim(),
            ["helpDeskPhone"] = string.Empty,
            ["applicationName"] = DefaultApplicationName,
            ["operationDate"] = DateTimeOffset.UtcNow.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture),
        };

    private static string? ResolveRecipient(
        AdManagementNotificationRecipientSource? source,
        string channel,
        AdUserCreatedNotificationEnqueueRequest request)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.Type))
        {
            return null;
        }

        var type = source.Type.Trim();
        var valueMaps = BuildAttributeValueMaps(request);

        return type switch
        {
            AdManagementNotificationRecipientSourceTypes.UserPrincipalName =>
                request.CreatedUser.UserPrincipalName,
            AdManagementNotificationRecipientSourceTypes.MailAttribute =>
                valueMaps.ByAttributeName.GetValueOrDefault("mail")
                ?? valueMaps.ByLogicalField.GetValueOrDefault("mail"),
            AdManagementNotificationRecipientSourceTypes.MappedAttribute =>
                ResolveMappedAttributeRecipient(source.Value, request, valueMaps),
            AdManagementNotificationRecipientSourceTypes.AdAttribute =>
                ResolveAdAttributeRecipient(source.Value, valueMaps),
            _ => null,
        };
    }

    private static string? ResolveMappedAttributeRecipient(
        string? mappingReference,
        AdUserCreatedNotificationEnqueueRequest request,
        AttributeValueMaps valueMaps)
    {
        if (string.IsNullOrWhiteSpace(mappingReference))
        {
            return null;
        }

        var reference = mappingReference.Trim();
        var mapping = request.AttributeMappings.FirstOrDefault(item =>
            string.Equals(item.Id.ToString(), reference, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.LogicalField, reference, StringComparison.OrdinalIgnoreCase));

        if (mapping is null)
        {
            return null;
        }

        return valueMaps.ByLogicalField.GetValueOrDefault(mapping.LogicalField)
            ?? valueMaps.ByAttributeName.GetValueOrDefault(mapping.AttributeName);
    }

    private static string? ResolveAdAttributeRecipient(string? attributeName, AttributeValueMaps valueMaps)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        return valueMaps.ByAttributeName.GetValueOrDefault(attributeName.Trim());
    }

    private static AttributeValueMaps BuildAttributeValueMaps(AdUserCreatedNotificationEnqueueRequest request)
    {
        var byLogicalField = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byAttributeName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapped in request.CreateRequest.MappedAttributes)
        {
            var value = ExtractMappedValue(mapped.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            byLogicalField[mapped.LogicalField.Trim()] = value;
        }

        foreach (var mapping in request.AttributeMappings)
        {
            if (!byLogicalField.TryGetValue(mapping.LogicalField, out var value))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(mapping.AttributeName))
            {
                byAttributeName[mapping.AttributeName.Trim()] = value;
            }
        }

        return new AttributeValueMaps(byLogicalField, byAttributeName);
    }

    private static string? ExtractMappedValue(object? value) =>
        value switch
        {
            null => null,
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            IEnumerable<string> values => values.FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item))?.Trim(),
            _ => string.IsNullOrWhiteSpace(value.ToString()) ? null : value.ToString()!.Trim(),
        };

    private static bool IsRecipientValid(string channel, string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return false;
        }

        if (string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _ = new MailAddress(recipient);
                return true;
            }
            catch
            {
                return EmailPattern().IsMatch(recipient);
            }
        }

        var trimmed = recipient.Trim();
        return trimmed.Length >= 3;
    }

    private static string MaskRecipientForLog(string channel, string recipient) =>
        string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase)
            ? NotificationRecipientMasker.MaskEmail(recipient)
            : NotificationRecipientMasker.MaskPhone(recipient);

    private sealed record ChannelEnqueueResult(int QueuedCount, int SkippedCount, IReadOnlyList<string> Messages);

    private sealed record AttributeValueMaps(
        IReadOnlyDictionary<string, string> ByLogicalField,
        IReadOnlyDictionary<string, string> ByAttributeName);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

}
