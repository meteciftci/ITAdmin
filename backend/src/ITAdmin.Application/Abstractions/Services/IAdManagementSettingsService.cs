using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdManagementSettingsService
{
    Task<AdManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<UpdateAdManagementSettingsResult> UpdateSettingsAsync(
        UpdateAdManagementSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<AdManagementValidationResult> ValidateCandidateAsync(
        UpdateAdManagementSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<AdManagementConnectionParameters?> GetConnectionParametersAsync(
        CancellationToken cancellationToken = default);

    Task RecordValidationResultAsync(
        AdManagementValidationResult result,
        AdManagementValidationRequest request,
        string? primaryDomainController,
        CancellationToken cancellationToken = default);
}
