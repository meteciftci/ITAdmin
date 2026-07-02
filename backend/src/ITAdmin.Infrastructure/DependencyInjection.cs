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
        // AD directory operations are split into focused, domain-scoped services that share
        // low-level LDAP plumbing via AdDirectoryServiceBase.
        services.AddScoped<AdUsersDirectoryService>();
        services.AddScoped<IAdUserDirectoryService>(sp => sp.GetRequiredService<AdUsersDirectoryService>());
        services.AddScoped<IAdUserAccountOperationService>(sp => sp.GetRequiredService<AdUsersDirectoryService>());
        services.AddScoped<IAdUserGroupMembershipService>(sp => sp.GetRequiredService<AdUsersDirectoryService>());
        services.AddScoped<IAdUserOuMoveService>(sp => sp.GetRequiredService<AdUsersDirectoryService>());
        services.AddScoped<IAdUserManagerUpdateService>(sp => sp.GetRequiredService<AdUsersDirectoryService>());
        services.AddScoped<IAdUserAccountExpirationUpdateService>(sp =>
            sp.GetRequiredService<AdUsersDirectoryService>());

        services.AddScoped<AdGroupsDirectoryService>();
        services.AddScoped<IAdGroupDirectoryService>(sp => sp.GetRequiredService<AdGroupsDirectoryService>());

        services.AddScoped<AdComputersDirectoryService>();
        services.AddScoped<IAdComputerDirectoryService>(sp => sp.GetRequiredService<AdComputersDirectoryService>());
        services.AddScoped<IAdComputerAccountOperationService>(sp =>
            sp.GetRequiredService<AdComputersDirectoryService>());
        services.AddScoped<IAdComputerUpdateService>(sp => sp.GetRequiredService<AdComputersDirectoryService>());
        services.AddScoped<IAdComputerOuMoveService>(sp => sp.GetRequiredService<AdComputersDirectoryService>());
        services.AddScoped<IAdComputerDeleteService>(sp => sp.GetRequiredService<AdComputersDirectoryService>());
        services.AddScoped<IAdComputerGroupMembershipService>(sp =>
            sp.GetRequiredService<AdComputersDirectoryService>());

        services.AddScoped<IAdDeletedObjectRestoreCommandRunner, AdDeletedObjectRestorePowerShellCommandRunner>();
        services.AddScoped<AdDeletedObjectsDirectoryService>();
        services.AddScoped<IAdDeletedObjectDirectoryService>(sp =>
            sp.GetRequiredService<AdDeletedObjectsDirectoryService>());
        services.AddScoped<IAdDeletedObjectRestoreService>(sp =>
            sp.GetRequiredService<AdDeletedObjectsDirectoryService>());
        services.AddScoped<IAdwsPortConnectivityChecker, AdwsPortConnectivityChecker>();
        services.AddScoped<IAdDeletedObjectRestoreReadinessPowerShellProbe, AdDeletedObjectRestoreReadinessPowerShellProbe>();
        services.AddScoped<IAdDeletedObjectRestoreReadinessService, AdDeletedObjectRestoreReadinessService>();
        services.AddScoped<IDirectoryUserLookupReadinessService, DirectoryUserLookupReadinessService>();
        services.AddScoped<IDirectoryOrganizationalUnitLookupReadinessService, DirectoryOrganizationalUnitLookupReadinessService>();
        services.AddScoped<IDirectoryOrganizationalUnitLookupService, DirectoryOrganizationalUnitLookupService>();
        services.AddScoped<AdOrganizationalUnitsDirectoryService>();
        services.AddScoped<IAdOrganizationalUnitDirectoryService>(sp =>
            sp.GetRequiredService<AdOrganizationalUnitsDirectoryService>());

        services.AddScoped<ISmsProviderAdapter, CustomHttpSmsAdapter>();
        services.AddScoped<IEmailProviderAdapter, SmtpEmailProviderAdapter>();
        services.AddScoped<ISmsProviderRegistry, SmsProviderRegistry>();
        services.AddScoped<IEmailProviderRegistry, EmailProviderRegistry>();

        return services;
    }
}
