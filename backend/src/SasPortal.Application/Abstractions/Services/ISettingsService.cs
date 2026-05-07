using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface ISettingsService
{
    Task<SettingsOverview> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<UpdateSettingsResult> UpdateLdapSettingsAsync(
        UpdateLdapSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<ValidateLdapSettingsResult> ValidateLdapSettingsAsync(
        ValidateLdapSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateSettingsResult> UpdateApplicationSettingsAsync(
        UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken = default);
}
