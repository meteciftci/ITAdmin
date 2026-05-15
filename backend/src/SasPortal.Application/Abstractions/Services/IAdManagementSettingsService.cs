using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdManagementSettingsService
{
    Task<AdManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<UpdateAdManagementSettingsResult> UpdateSettingsAsync(
        UpdateAdManagementSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<AdManagementConnectionParameters?> GetConnectionParametersAsync(
        CancellationToken cancellationToken = default);

    Task RecordValidationResultAsync(
        AdManagementValidationResult result,
        AdManagementValidationRequest request,
        string? primaryDomainController,
        CancellationToken cancellationToken = default);
}
