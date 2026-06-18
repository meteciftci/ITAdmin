namespace ITAdmin.Application.Common.Models.Notifications;

public sealed record NotificationTemplateCatalog(
    IReadOnlyList<NotificationTemplateCatalogModule> Modules);

public sealed record NotificationTemplateCatalogModule(
    string Key,
    IReadOnlyList<NotificationTemplateCatalogEvent> Events);

public sealed record NotificationTemplateCatalogEvent(
    string Key,
    IReadOnlyList<string> SupportedChannels,
    IReadOnlyList<NotificationTemplateCatalogVariable> Variables);

public sealed record NotificationTemplateCatalogVariable(
    string Key,
    string? Example = null);
