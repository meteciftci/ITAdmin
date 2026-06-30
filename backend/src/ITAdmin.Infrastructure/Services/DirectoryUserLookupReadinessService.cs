using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Infrastructure.Services;

public sealed class DirectoryUserLookupReadinessService(
    IAdManagementSettingsService settingsService) : IDirectoryUserLookupReadinessService
{
    public Task<DirectoryUserLookupReadinessResult> CheckAsync(CancellationToken cancellationToken = default) =>
        AdDirectoryLookupReadinessChecker.CheckAsync(settingsService, cancellationToken);
}

public sealed class DirectoryOrganizationalUnitLookupReadinessService(
    IAdManagementSettingsService settingsService) : IDirectoryOrganizationalUnitLookupReadinessService
{
    public async Task<DirectoryUserLookupReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var result = await AdDirectoryLookupReadinessChecker.CheckAsync(settingsService, cancellationToken);
        if (result.IsReady)
        {
            return result;
        }

        return result.Reason switch
        {
            "ModuleDisabled" => result with
            {
                Message = "AD OU arama için AD Yönetim modülü etkin olmalıdır.",
            },
            _ => result with
            {
                Message = "AD OU arama için AD Yönetim bağlantısı yapılandırılmalıdır.",
            },
        };
    }
}
