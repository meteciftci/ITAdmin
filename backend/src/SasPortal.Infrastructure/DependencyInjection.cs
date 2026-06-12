using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Infrastructure.Notifications.Email;
using SasPortal.Infrastructure.Notifications.Sms;
using SasPortal.Infrastructure.Security;
using SasPortal.Infrastructure.Services;

namespace SasPortal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtectionSettings = DataProtectionSettings.Load(configuration);

        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName(dataProtectionSettings.ApplicationName);

        if (dataProtectionSettings.PersistKeysToFileSystem)
        {
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionSettings.KeysPath!));
        }

        if (dataProtectionSettings.ProtectKeysWithCertificate)
        {
            var certificate = DataProtectionCertificateLoader.LoadByThumbprint(
                dataProtectionSettings.CertificateThumbprint!);
            dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
        }

        services.AddHttpClient("NotificationProviders");

        services.AddScoped<ISecretProtector, SecretProtector>();
        services.AddScoped<ILdapService, LdapService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAdManagementValidationService, AdManagementValidationService>();
        services.AddScoped<AdUserDirectoryService>();
        services.AddScoped<IAdUserDirectoryService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdUserAccountOperationService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdUserGroupMembershipService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdUserOuMoveService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdUserManagerUpdateService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdUserAccountExpirationUpdateService>(sp =>
            sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdGroupDirectoryService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdComputerDirectoryService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdComputerAccountOperationService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdComputerUpdateService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdComputerOuMoveService>(sp => sp.GetRequiredService<AdUserDirectoryService>());

        services.AddScoped<ISmsProviderAdapter, CustomHttpSmsAdapter>();
        services.AddScoped<IEmailProviderAdapter, SmtpEmailProviderAdapter>();
        services.AddScoped<ISmsProviderRegistry, SmsProviderRegistry>();
        services.AddScoped<IEmailProviderRegistry, EmailProviderRegistry>();

        return services;
    }
}
