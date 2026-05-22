using Microsoft.Extensions.DependencyInjection;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Notifications;

namespace SasPortal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        return services;
    }
}
