using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Notifications;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicenseRenewalReminderProcessor(
    AppDbContext context,
    INotificationTemplateRenderer templateRenderer) : ILicenseRenewalReminderProcessor
{
    private const string ApplicationName = "ITAdmin";
    private const string RelatedEntityType = "LicensePackage";
    private const string SystemActor = "system";

    public async Task<LicenseRenewalReminderRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = await context.LicenseManagementSettings
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            return new LicenseRenewalReminderRunResult(0, 0, 0, 0, "License management settings do not exist.");
        }

        var allRecipients = LicenseRenewalRecipientParser.Parse(
            settings.DefaultRenewalRecipients,
            settings.DefaultRenewalCcRecipients);
        var validRecipients = allRecipients.Where(LicenseRenewalRecipientParser.IsValidEmail).ToArray();
        var invalidRecipientCount = allRecipients.Count - validRecipients.Length;
        if (validRecipients.Length == 0)
        {
            return await CompleteRunAsync(settings, new LicenseRenewalReminderRunResult(
                0,
                0,
                0,
                invalidRecipientCount,
                "No valid renewal notification recipient is configured."), "Skipped", cancellationToken);
        }

        var template = await context.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ModuleKey == NotificationModuleKeys.LicenseManagement
                    && x.EventKey == NotificationEventKeys.LicenseRenewalDue
                    && x.Channel == NotificationChannels.Email
                    && x.IsEnabled,
                cancellationToken);
        if (template is null)
        {
            return await CompleteRunAsync(settings, new LicenseRenewalReminderRunResult(
                0,
                0,
                0,
                invalidRecipientCount,
                "An active license renewal email template is not configured."), "Skipped", cancellationToken);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(Math.Max(1, settings.DefaultRenewalReminderDays));
        var duePackages = await context.LicensePackages
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Purchase)
            .Where(x =>
                x.RenewalRequired
                && x.RenewalDate != null
                && x.RenewalDate <= cutoff
                && x.Status != LicensePackageStatus.Cancelled
                && x.Status != LicensePackageStatus.Archived
                && !context.LicensePackages.Any(renewal => renewal.PreviousPackageId == x.Id))
            .OrderBy(x => x.RenewalDate)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var queued = 0;
        var existing = 0;
        foreach (var package in duePackages)
        {
            var renewalDate = package.RenewalDate!.Value;
            var variables = BuildVariables(package, renewalDate, today);
            var subject = templateRenderer.Render(template.SubjectTemplate ?? string.Empty, variables).Trim();
            var body = templateRenderer.Render(template.BodyTemplate, variables).Trim();
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            foreach (var recipient in validRecipients)
            {
                var inserted = await TryInsertOutboxAsync(
                    package.Id,
                    renewalDate,
                    recipient,
                    subject,
                    body,
                    cancellationToken);
                if (inserted)
                {
                    queued++;
                }
                else
                {
                    existing++;
                }
            }
        }

        return await CompleteRunAsync(settings, new LicenseRenewalReminderRunResult(
            duePackages.Count,
            queued,
            existing,
            invalidRecipientCount,
            $"Renewal scan completed. {queued} notification(s) queued."), "Success", cancellationToken);
    }

    private async Task<LicenseRenewalReminderRunResult> CompleteRunAsync(
        LicenseManagementSettings settings,
        LicenseRenewalReminderRunResult result,
        string status,
        CancellationToken cancellationToken)
    {
        settings.LastRenewalReminderRunAt = DateTime.UtcNow;
        settings.LastRenewalReminderStatus = status;
        settings.LastRenewalReminderDueCount = result.DuePackageCount;
        settings.LastRenewalReminderQueuedCount = result.QueuedCount;
        settings.LastRenewalReminderMessage = result.Message.Length <= 1000
            ? result.Message
            : result.Message[..1000];
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<bool> TryInsertOutboxAsync(
        Guid packageId,
        DateOnly renewalDate,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var correlationId = BuildCorrelationId(packageId, renewalDate, recipient);
        if (context.Database.IsNpgsql())
        {
            var now = DateTimeOffset.UtcNow;
            var maskedRecipient = NotificationRecipientMasker.MaskEmail(recipient);
            var affected = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO notification_outbox
                    (id, channel, provider_key, recipient, recipient_masked, subject, body, status,
                     priority, attempt_count, max_attempts, next_attempt_at, related_module,
                     related_event, related_entity_type, related_entity_id, correlation_id,
                     created_at, created_by)
                VALUES
                    (gen_random_uuid(), {NotificationChannels.Email}, {NotificationProviderKeys.Smtp},
                     {recipient}, {maskedRecipient}, {subject}, {body}, {NotificationOutboxStatuses.Pending},
                     10, 0, 3, {now}, {NotificationModuleKeys.LicenseManagement},
                     {NotificationEventKeys.LicenseRenewalDue}, {RelatedEntityType}, {packageId.ToString()},
                     {correlationId}, {now}, {SystemActor})
                ON CONFLICT (correlation_id)
                    WHERE related_module = 'LicenseManagement'
                      AND related_event = 'RenewalDue'
                      AND correlation_id IS NOT NULL
                DO NOTHING;
                """,
                cancellationToken);
            return affected == 1;
        }

        if (await context.NotificationOutboxItems.AnyAsync(x => x.CorrelationId == correlationId, cancellationToken))
        {
            return false;
        }

        var entity = new NotificationOutbox
        {
            Channel = NotificationChannels.Email,
            ProviderKey = NotificationProviderKeys.Smtp,
            Recipient = recipient,
            RecipientMasked = NotificationRecipientMasker.MaskEmail(recipient),
            Subject = subject,
            Body = body,
            Status = NotificationOutboxStatuses.Pending,
            Priority = 10,
            AttemptCount = 0,
            MaxAttempts = 3,
            NextAttemptAt = DateTimeOffset.UtcNow,
            RelatedModule = NotificationModuleKeys.LicenseManagement,
            RelatedEvent = NotificationEventKeys.LicenseRenewalDue,
            RelatedEntityType = RelatedEntityType,
            RelatedEntityId = packageId.ToString(),
            CorrelationId = correlationId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = SystemActor,
        };
        await context.NotificationOutboxItems.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static IReadOnlyDictionary<string, object?> BuildVariables(
        LicensePackage package,
        DateOnly renewalDate,
        DateOnly today) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["applicationName"] = ApplicationName,
            ["productName"] = package.Product.Name,
            ["purchaseTitle"] = package.Purchase.Title,
            ["renewalDate"] = renewalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["daysRemaining"] = renewalDate.DayNumber - today.DayNumber,
            ["quantity"] = package.Quantity,
            ["licenseType"] = package.LicenseType.ToString(),
        };

    private static string BuildCorrelationId(Guid packageId, DateOnly renewalDate, string recipient)
    {
        var recipientHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(recipient.Trim().ToLowerInvariant())));
        return $"renewal:{packageId:N}:{renewalDate:yyyyMMdd}:{recipientHash[..16]}";
    }
}
