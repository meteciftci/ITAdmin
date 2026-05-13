using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.Fakes;

/// <summary>
/// Minimal <see cref="ISettingsService"/> for <see cref="SasPortal.Persistence.Services.AuthService"/> unit tests.
/// </summary>
public sealed class AuthServiceSettingsFake : ISettingsService
{
    public SessionSecuritySettings SessionSecurity { get; set; } = SessionSecurityDefaults.AsSettings();

    public Task<SessionSecuritySettings> GetSessionSecuritySettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SessionSecurity);

    public Task<AuthSessionOptions> GetAuthSessionOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthSessionOptions(SessionSecurity.RememberMeEnabled));

    public Task<SettingsOverview> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<BrandingSettings> GetBrandingSettingsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<UpdateSettingsResult> UpdateLdapSettingsAsync(
        UpdateLdapSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ValidateLdapSettingsResult> ValidateLdapSettingsAsync(
        ValidateLdapSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<UpdateSettingsResult> UpdateApplicationSettingsAsync(
        UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<UpdateSettingsResult> UpdateSessionSecuritySettingsAsync(
        UpdateSessionSecuritySettingsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<BrandingLogoUploadResult> UploadBrandingLogoAsync(
        UploadBrandingLogoRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<BrandingFaviconUploadResult> UploadBrandingFaviconAsync(
        UploadBrandingFaviconRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
