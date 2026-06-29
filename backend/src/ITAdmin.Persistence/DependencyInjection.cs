using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Options;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using ITAdmin.Persistence.Services.LicenseManagement;

namespace ITAdmin.Persistence;

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
        services.AddScoped<ISetupPreflightService, SetupPreflightService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<ISecurityLogService, SecurityLogService>();
        services.AddScoped<ISecurityLogWriter, SecurityLogWriter>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IReadinessService, ReadinessService>();
        services.AddScoped<IAdOperationLogService, AdOperationLogService>();
        services.AddScoped<IAdManagementSettingsService, AdManagementSettingsService>();
        services.AddScoped<IAdAttributeMappingService, AdAttributeMappingService>();
        services.AddScoped<INotificationProviderSettingsService, NotificationProviderSettingsService>();
        services.AddScoped<INotificationSender, NotificationSender>();
        services.AddScoped<INotificationOutboxService, NotificationOutboxService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<IAdManagementNotificationEnqueueService, AdManagementNotificationEnqueueService>();
        services.AddScoped<INotificationOutboxBatchProcessor, NotificationOutboxBatchProcessor>();
        services.AddScoped<ILicenseManagementOverviewService, LicenseManagementOverviewService>();
        services.AddScoped<ILicenseCompanyService, LicenseCompanyService>();
        services.AddScoped<ILicensedProductService, LicensedProductService>();
        services.AddScoped<ILicensePurchaseService, LicensePurchaseService>();
        services.AddScoped<ILicensePackageService, LicensePackageService>();
        services.AddScoped<ILicenseRequestService, LicenseRequestService>();
        services.AddScoped<ILicenseManagementSettingsService, LicenseManagementSettingsService>();
        services.Configure<NotificationOutboxOptions>(configuration.GetSection(NotificationOutboxOptions.SectionName));

        return services;
    }
}
