using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Infrastructure.Notifications.Email;
using ITAdmin.Infrastructure.Notifications.Sms;
using ITAdmin.Infrastructure.Security;
using ITAdmin.Infrastructure.Services;

namespace ITAdmin.Infrastructure;

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
        services.AddScoped<IAdComputerDeleteService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdComputerGroupMembershipService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdDeletedObjectDirectoryService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdDeletedObjectRestoreCommandRunner, AdDeletedObjectRestorePowerShellCommandRunner>();
        services.AddScoped<IAdDeletedObjectRestoreService>(sp => sp.GetRequiredService<AdUserDirectoryService>());
        services.AddScoped<IAdwsPortConnectivityChecker, AdwsPortConnectivityChecker>();
        services.AddScoped<IAdDeletedObjectRestoreReadinessPowerShellProbe, AdDeletedObjectRestoreReadinessPowerShellProbe>();
        services.AddScoped<IAdDeletedObjectRestoreReadinessService, AdDeletedObjectRestoreReadinessService>();
        services.AddScoped<IDirectoryUserLookupReadinessService, DirectoryUserLookupReadinessService>();
        services.AddScoped<IAdOrganizationalUnitDirectoryService>(sp => sp.GetRequiredService<AdUserDirectoryService>());

        services.AddScoped<ISmsProviderAdapter, CustomHttpSmsAdapter>();
        services.AddScoped<IEmailProviderAdapter, SmtpEmailProviderAdapter>();
        services.AddScoped<ISmsProviderRegistry, SmsProviderRegistry>();
        services.AddScoped<IEmailProviderRegistry, EmailProviderRegistry>();

        return services;
    }
}
