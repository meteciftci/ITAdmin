using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Application.Notifications;

public sealed class StaticNotificationTemplateCatalogProvider : INotificationTemplateCatalogProvider
{
    private static readonly NotificationTemplateCatalog Catalog = BuildCatalog();

    public NotificationTemplateCatalog GetCatalog() => Catalog;

    public bool TryGetEvent(
        string moduleKey,
        string eventKey,
        out NotificationTemplateCatalogEvent? catalogEvent)
    {
        catalogEvent = null;
        if (string.IsNullOrWhiteSpace(moduleKey) || string.IsNullOrWhiteSpace(eventKey))
        {
            return false;
        }

        var module = Catalog.Modules.FirstOrDefault(
            m => string.Equals(m.Key, moduleKey.Trim(), StringComparison.OrdinalIgnoreCase));
        if (module is null)
        {
            return false;
        }

        catalogEvent = module.Events.FirstOrDefault(
            e => string.Equals(e.Key, eventKey.Trim(), StringComparison.OrdinalIgnoreCase));

        return catalogEvent is not null;
    }

    public string? ValidateTemplateKeys(string moduleKey, string eventKey, string channel)
    {
        if (!TryGetEvent(moduleKey, eventKey, out var catalogEvent) || catalogEvent is null)
        {
            return "Module and event are not defined in the notification template catalog.";
        }

        if (!IsSupportedChannel(catalogEvent, channel))
        {
            return "The selected channel is not supported for this event.";
        }

        return null;
    }

    private static bool IsSupportedChannel(NotificationTemplateCatalogEvent catalogEvent, string channel) =>
        catalogEvent.SupportedChannels.Any(
            supported => string.Equals(supported, channel.Trim(), StringComparison.OrdinalIgnoreCase));

    private static NotificationTemplateCatalog BuildCatalog() =>
        new(
        [
            new NotificationTemplateCatalogModule(
                "System",
                [
                    new NotificationTemplateCatalogEvent(
                        "GenericNotification",
                        [NotificationChannels.Sms, NotificationChannels.Email],
                        Variables(
                            "applicationName",
                            "operationDate",
                            "message",
                            "actorName")),
                ]),
            new NotificationTemplateCatalogModule(
                "AdManagement",
                [
                    new NotificationTemplateCatalogEvent(
                        "UserCreated",
                        [NotificationChannels.Sms, NotificationChannels.Email],
                        AdManagementEventVariables()),
                    new NotificationTemplateCatalogEvent(
                        "UserEnabled",
                        [NotificationChannels.Sms, NotificationChannels.Email],
                        AdManagementEventVariables()),
                    new NotificationTemplateCatalogEvent(
                        "UserDisabled",
                        [NotificationChannels.Sms, NotificationChannels.Email],
                        AdManagementEventVariables()),
                    new NotificationTemplateCatalogEvent(
                        "UserUnlocked",
                        [NotificationChannels.Sms, NotificationChannels.Email],
                        AdManagementEventVariables()),
                    new NotificationTemplateCatalogEvent(
                        "PasswordReset",
                        [NotificationChannels.Sms, NotificationChannels.Email],
                        Variables(
                            "displayName",
                            "username",
                            "upn",
                            "operationDate",
                            "helpDeskPhone",
                            "applicationName")),
                ]),
        ]);

    private static IReadOnlyList<NotificationTemplateCatalogVariable> Variables(params string[] keys) =>
        keys.Select(key => new NotificationTemplateCatalogVariable(key)).ToArray();

    private static IReadOnlyList<NotificationTemplateCatalogVariable> AdManagementEventVariables() =>
        Variables(
            "displayName",
            "username",
            "upn",
            "department",
            "helpDeskPhone",
            "applicationName",
            "operationDate",
            "actorName");
}
