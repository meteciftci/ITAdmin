using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Audit;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models.Notifications;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class NotificationTemplateService(
    AppDbContext context,
    INotificationTemplateCatalogProvider catalogProvider) : INotificationTemplateService
{
    private const int AuditIpAddressMaxLength = 64;
    private const int AuditUserAgentMaxLength = 1024;

    public async Task<IReadOnlyList<NotificationTemplateListItem>> GetListAsync(
        NotificationTemplateListQuery query,
        CancellationToken cancellationToken = default)
    {
        var itemsQuery = context.NotificationTemplates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ModuleKey))
        {
            itemsQuery = itemsQuery.Where(x => x.ModuleKey == query.ModuleKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.EventKey))
        {
            itemsQuery = itemsQuery.Where(x => x.EventKey == query.EventKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            itemsQuery = itemsQuery.Where(x => x.Channel == query.Channel.Trim());
        }

        return await itemsQuery
            .OrderBy(x => x.ModuleKey)
            .ThenBy(x => x.EventKey)
            .ThenBy(x => x.Channel)
            .Select(x => new NotificationTemplateListItem(
                x.Id,
                x.ModuleKey,
                x.EventKey,
                x.Channel,
                x.Name,
                x.IsEnabled,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationTemplateModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<NotificationTemplateOperationResult> CreateAsync(
        CreateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRequest(
            request.ModuleKey,
            request.EventKey,
            request.Channel,
            request.Name,
            request.BodyTemplate);
        if (validationError is not null)
        {
            return new NotificationTemplateOperationResult(false, validationError);
        }

        var moduleKey = request.ModuleKey.Trim();
        var eventKey = request.EventKey.Trim();
        var channel = NormalizeChannel(request.Channel);

        var exists = await context.NotificationTemplates
            .AnyAsync(
                x => x.ModuleKey == moduleKey && x.EventKey == eventKey && x.Channel == channel,
                cancellationToken);
        if (exists)
        {
            return new NotificationTemplateOperationResult(
                false,
                "A template with the same module, event and channel already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new NotificationTemplate
        {
            ModuleKey = moduleKey,
            EventKey = eventKey,
            Channel = channel,
            Name = request.Name.Trim(),
            IsEnabled = request.IsEnabled,
            SubjectTemplate = TrimOrNull(request.SubjectTemplate),
            BodyTemplate = request.BodyTemplate.Trim(),
            Description = TrimOrNull(request.Description),
            CreatedAt = now,
            CreatedBy = request.ActorUserName,
        };

        await context.NotificationTemplates.AddAsync(entity, cancellationToken);
        await WriteAuditAsync(
            "Create",
            entity,
            $"Notification template created. Module: {entity.ModuleKey}. Event: {entity.EventKey}. Channel: {entity.Channel}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return new NotificationTemplateOperationResult(true, "Notification template created.", Map(entity));
    }

    public async Task<NotificationTemplateOperationResult> UpdateAsync(
        Guid id,
        UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.NotificationTemplates
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return new NotificationTemplateOperationResult(false, "Notification template was not found.");
        }

        var validationError = ValidateRequest(
            request.ModuleKey,
            request.EventKey,
            request.Channel,
            request.Name,
            request.BodyTemplate);
        if (validationError is not null)
        {
            return new NotificationTemplateOperationResult(false, validationError);
        }

        var moduleKey = request.ModuleKey.Trim();
        var eventKey = request.EventKey.Trim();
        var channel = NormalizeChannel(request.Channel);

        var duplicate = await context.NotificationTemplates
            .AnyAsync(
                x => x.Id != id && x.ModuleKey == moduleKey && x.EventKey == eventKey && x.Channel == channel,
                cancellationToken);
        if (duplicate)
        {
            return new NotificationTemplateOperationResult(
                false,
                "A template with the same module, event and channel already exists.");
        }

        var before = CloneSnapshot(entity);
        entity.ModuleKey = moduleKey;
        entity.EventKey = eventKey;
        entity.Channel = channel;
        entity.Name = request.Name.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.SubjectTemplate = TrimOrNull(request.SubjectTemplate);
        entity.BodyTemplate = request.BodyTemplate.Trim();
        entity.Description = TrimOrNull(request.Description);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = request.ActorUserName;

        var changes = BuildTemplateAuditChanges(before, entity);
        var description = AuditChangeSummaryBuilder.BuildUpdateDescription(
            $"Notification template updated. Module: {entity.ModuleKey}. Event: {entity.EventKey}. Channel: {entity.Channel}.",
            changes);

        await WriteAuditAsync(
            "Update",
            entity,
            description,
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return new NotificationTemplateOperationResult(true, "Notification template updated.", Map(entity));
    }

    private string? ValidateRequest(
        string moduleKey,
        string eventKey,
        string channel,
        string name,
        string bodyTemplate)
    {
        if (string.IsNullOrWhiteSpace(moduleKey)
            || string.IsNullOrWhiteSpace(eventKey)
            || string.IsNullOrWhiteSpace(channel)
            || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(bodyTemplate))
        {
            return "Module, event, channel, name and body template are required.";
        }

        var normalizedChannel = NormalizeChannel(channel);

        if (!IsValidChannel(normalizedChannel))
        {
            return "Notification channel is invalid.";
        }

        return catalogProvider.ValidateTemplateKeys(
            moduleKey.Trim(),
            eventKey.Trim(),
            normalizedChannel);
    }

    private static bool IsValidChannel(string channel) =>
        string.Equals(channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase)
        || string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeChannel(string channel) =>
        string.Equals(channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase)
            ? NotificationChannels.Sms
            : string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase)
                ? NotificationChannels.Email
                : channel.Trim();

    private static List<AuditFieldChange> BuildTemplateAuditChanges(
        TemplateSnapshot before,
        NotificationTemplate after)
    {
        var changes = new List<AuditFieldChange>
        {
            AuditChangeSummaryBuilder.PublicField("Name", before.Name, after.Name),
            AuditChangeSummaryBuilder.PublicField("IsEnabled", before.IsEnabled.ToString(), after.IsEnabled.ToString()),
            AuditChangeSummaryBuilder.PublicField("SubjectTemplate", before.SubjectTemplate, after.SubjectTemplate, treatAsLongText: true),
            AuditChangeSummaryBuilder.PublicField("BodyTemplate", before.BodyTemplate, after.BodyTemplate, treatAsLongText: true),
            AuditChangeSummaryBuilder.PublicField("Description", before.Description, after.Description, treatAsLongText: true),
        };

        return changes
            .Where(change =>
                change.IsSensitive
                || change.DisplayMode is AuditChangeDisplayMode.ChangedOnly or AuditChangeDisplayMode.Cleared
                || !string.Equals(change.OldValue, change.NewValue, StringComparison.Ordinal))
            .ToList();
    }

    private static TemplateSnapshot CloneSnapshot(NotificationTemplate entity) =>
        new(entity.Name, entity.IsEnabled, entity.SubjectTemplate, entity.BodyTemplate, entity.Description);

    private sealed record TemplateSnapshot(
        string Name,
        bool IsEnabled,
        string? SubjectTemplate,
        string BodyTemplate,
        string? Description);

    private static NotificationTemplateModel Map(NotificationTemplate entity) =>
        new(
            entity.Id,
            entity.ModuleKey,
            entity.EventKey,
            entity.Channel,
            entity.Name,
            entity.IsEnabled,
            entity.SubjectTemplate,
            entity.BodyTemplate,
            entity.Description,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);

    private async Task WriteAuditAsync(
        string action,
        NotificationTemplate entity,
        string description,
        Guid? actorUserId,
        string? actorUserName,
        string? actorIpAddress,
        string? actorUserAgent,
        CancellationToken cancellationToken)
    {
        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = action,
                EntityName = "NotificationTemplate",
                EntityId = entity.Id.ToString(),
                Description = description,
                ActorUserId = actorUserId,
                ActorUserName = actorUserName,
                IpAddress = TruncateNullable(actorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(actorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
