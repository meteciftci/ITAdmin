using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.Notifications;
using ITAdmin.Application.Common.Notifications;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed partial class AdManagementNotificationEnqueueService(
    AppDbContext context,
    INotificationOutboxService outboxService,
    INotificationTemplateRenderer templateRenderer,
    ILogger<AdManagementNotificationEnqueueService> logger) : IAdManagementNotificationEnqueueService
{
    private const string DefaultApplicationName = "ITAdmin";

    public Task<AdManagementNotificationSummary> EnqueueUserCreatedAsync(
        AdUserCreatedNotificationEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        var mappedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var attributeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapped in request.CreateRequest.MappedAttributes)
        {
            var value = ExtractMappedValue(mapped.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                mappedValues[mapped.LogicalField.Trim()] = value;
            }
        }

        foreach (var mapping in request.AttributeMappings)
        {
            if (!mappedValues.TryGetValue(mapping.LogicalField, out var value))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(mapping.AttributeName))
            {
                attributeValues[mapping.AttributeName.Trim()] = value;
            }
        }

        var userContext = new AdManagementNotificationUserContext(
            request.CreatedUser.Id,
            request.CreatedUser.SamAccountName,
            request.CreatedUser.UserPrincipalName,
            request.CreatedUser.DisplayName,
            attributeValues.GetValueOrDefault("mail"),
            request.CreateRequest.Department?.Trim(),
            mappedValues,
            attributeValues,
            request.AttributeMappings,
            request.ActorUserName);

        return EnqueueForEventAsync(
            AdManagementNotificationEventKeys.UserCreated,
            userContext,
            request.CreatedUser.Id,
            cancellationToken);
    }

    public Task<AdManagementNotificationSummary> EnqueueAccountOperationAsync(
        AdManagementAccountOperationNotificationRequest request,
        CancellationToken cancellationToken = default) =>
        EnqueueForEventAsync(
            request.EventKey,
            request.UserContext,
            request.UserContext.UserId,
            cancellationToken);

    private async Task<AdManagementNotificationSummary> EnqueueForEventAsync(
        string eventKey,
        AdManagementNotificationUserContext userContext,
        string relatedEntityId,
        CancellationToken cancellationToken)
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

            var enabledRules = notificationSettings.Rules
                .Where(rule =>
                    rule.IsEnabled
                    && string.Equals(rule.EventKey, eventKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (enabledRules.Count == 0)
            {
                return new AdManagementNotificationSummary(0, 0, messages);
            }

            foreach (var rule in enabledRules)
            {
                var channelResult = await TryEnqueueRuleAsync(
                    rule,
                    eventKey,
                    userContext,
                    relatedEntityId,
                    cancellationToken);
                queuedCount += channelResult.QueuedCount;
                skippedCount += channelResult.SkippedCount;
                messages.AddRange(channelResult.Messages);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AD management notification enqueue failed. EventKey={EventKey} RelatedEntityId={RelatedEntityId}",
                eventKey,
                relatedEntityId);
            messages.Add("Notification enqueue failed.");
            skippedCount += 1;
        }

        return new AdManagementNotificationSummary(queuedCount, skippedCount, messages);
    }

    private async Task<ChannelEnqueueResult> TryEnqueueRuleAsync(
        AdManagementNotificationRule rule,
        string eventKey,
        AdManagementNotificationUserContext userContext,
        string relatedEntityId,
        CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        var channel = rule.Channel.Trim();

        var template = await FindActiveTemplateAsync(eventKey, channel, cancellationToken);
        if (template is null)
        {
            messages.Add(
                $"Active notification template was not found for {NotificationModuleKeys.AdManagement}/{eventKey}/{channel}.");
            return new ChannelEnqueueResult(0, 1, messages);
        }

        var recipient = AdManagementNotificationRecipientResolver.Resolve(
            rule.RecipientSource,
            channel,
            userContext);
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

        var variables = BuildTemplateVariables(eventKey, userContext);
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
                eventKey,
                RelatedEntityType: "AdUser",
                RelatedEntityId: relatedEntityId,
                CorrelationId: null,
                Priority: 0,
                MaxAttempts: null,
                CreatedBy: userContext.ActorUserName),
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
        string eventKey,
        string channel,
        CancellationToken cancellationToken)
    {
        var templates = await context.NotificationTemplates
            .AsNoTracking()
            .Where(x =>
                x.ModuleKey == NotificationModuleKeys.AdManagement
                && x.EventKey == eventKey
                && x.Channel == channel
                && x.IsEnabled)
            .ToListAsync(cancellationToken);

        return templates.Count == 0 ? null : templates[0];
    }

    private static IReadOnlyDictionary<string, object?> BuildTemplateVariables(
        string eventKey,
        AdManagementNotificationUserContext userContext)
    {
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["displayName"] = userContext.DisplayName ?? userContext.SamAccountName ?? string.Empty,
            ["username"] = userContext.SamAccountName ?? string.Empty,
            ["upn"] = userContext.UserPrincipalName ?? string.Empty,
            ["department"] = userContext.Department ?? string.Empty,
            ["helpDeskPhone"] = string.Empty,
            ["applicationName"] = DefaultApplicationName,
            ["operationDate"] = DateTimeOffset.UtcNow.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture),
            ["actorName"] = userContext.ActorUserName ?? string.Empty,
        };

        return variables;
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
            catch (FormatException)
            {
                // MailAddress rejects some valid-enough values; fall back to regex validation.
                return EmailPattern().IsMatch(recipient);
            }
        }

        return recipient.Trim().Length >= 3;
    }

    private static string MaskRecipientForLog(string channel, string recipient) =>
        string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase)
            ? NotificationRecipientMasker.MaskEmail(recipient)
            : NotificationRecipientMasker.MaskPhone(recipient);

    private sealed record ChannelEnqueueResult(int QueuedCount, int SkippedCount, IReadOnlyList<string> Messages);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();
}
