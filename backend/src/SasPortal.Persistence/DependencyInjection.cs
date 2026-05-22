using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;

namespace SasPortal.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<ISecurityLogService, SecurityLogService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IReadinessService, ReadinessService>();
        services.AddScoped<IAdOperationLogService, AdOperationLogService>();
        services.AddScoped<IAdManagementSettingsService, AdManagementSettingsService>();
        services.AddScoped<IAdAttributeMappingService, AdAttributeMappingService>();
        services.AddScoped<INotificationProviderSettingsService, NotificationProviderSettingsService>();
        services.AddScoped<INotificationSender, NotificationSender>();

        return services;
    }
}
