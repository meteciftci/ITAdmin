using ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Application.Abstractions.Notifications;

public interface INotificationTemplateCatalogProvider
{
    NotificationTemplateCatalog GetCatalog();

    bool TryGetEvent(
        string moduleKey,
        string eventKey,
        out NotificationTemplateCatalogEvent? catalogEvent);

    string? ValidateTemplateKeys(string moduleKey, string eventKey, string channel);
}
