using Microsoft.Extensions.DependencyInjection;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Notifications;

namespace ITAdmin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddSingleton<INotificationTemplateCatalogProvider, StaticNotificationTemplateCatalogProvider>();
        return services;
    }
}
