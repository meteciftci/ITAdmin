using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Infrastructure.Services;

public sealed class DirectoryUserLookupReadinessService(
    IAdManagementSettingsService settingsService) : IDirectoryUserLookupReadinessService
{
    private const string SuccessValidationStatus = "Ok";

    public async Task<DirectoryUserLookupReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        if (!settings.IsEnabled)
        {
            return new DirectoryUserLookupReadinessResult(
                false,
                "ModuleDisabled",
                "AD kullanıcı arama için AD Yönetim modülü etkin olmalıdır.");
        }

        var hasCoreFields = !string.IsNullOrWhiteSpace(settings.DomainFqdn)
            && !string.IsNullOrWhiteSpace(settings.NetbiosDomainName)
            && !string.IsNullOrWhiteSpace(settings.DefaultNamingContext)
            && !string.IsNullOrWhiteSpace(settings.BaseDn)
            && !string.IsNullOrWhiteSpace(settings.ServiceAccountUserName)
            && settings.HasServiceAccountPassword;

        if (!hasCoreFields)
        {
            return new DirectoryUserLookupReadinessResult(
                false,
                "AdManagementNotConfigured",
                "AD kullanıcı arama için AD Yönetim bağlantısı yapılandırılmalıdır.");
        }

        var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);
        if (connection is null
            || string.IsNullOrWhiteSpace(connection.ServiceAccountUserName)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountPassword))
        {
            return new DirectoryUserLookupReadinessResult(
                false,
                "AdManagementNotConfigured",
                "AD kullanıcı arama için AD Yönetim bağlantısı yapılandırılmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(settings.LastValidationStatus)
            || !string.Equals(
                settings.LastValidationStatus.Trim(),
                SuccessValidationStatus,
                StringComparison.OrdinalIgnoreCase))
        {
            return new DirectoryUserLookupReadinessResult(
                false,
                "AdManagementNotConfigured",
                "AD kullanıcı arama için AD Yönetim bağlantısı yapılandırılmalıdır.");
        }

        return new DirectoryUserLookupReadinessResult(true, "Ready", null);
    }
}
