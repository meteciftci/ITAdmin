using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface ISettingsService
{
    Task<SettingsOverview> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<BrandingSettings> GetBrandingSettingsAsync(CancellationToken cancellationToken = default);

    Task<UpdateSettingsResult> UpdateLdapSettingsAsync(
        UpdateLdapSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<ValidateLdapSettingsResult> ValidateLdapSettingsAsync(
        ValidateLdapSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateSettingsResult> UpdateApplicationSettingsAsync(
        UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateSettingsResult> UpdateSessionSecuritySettingsAsync(
        UpdateSessionSecuritySettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionSecuritySettings> GetSessionSecuritySettingsAsync(
        CancellationToken cancellationToken = default);

    Task<AuthSessionOptions> GetAuthSessionOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<BrandingLogoUploadResult> UploadBrandingLogoAsync(
        UploadBrandingLogoRequest request,
        CancellationToken cancellationToken = default);

    Task<BrandingFaviconUploadResult> UploadBrandingFaviconAsync(
        UploadBrandingFaviconRequest request,
        CancellationToken cancellationToken = default);
}
