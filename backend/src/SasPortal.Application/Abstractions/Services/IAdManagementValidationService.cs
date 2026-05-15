using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdManagementValidationService
{
    Task<AdManagementValidationResult> ValidateAsync(
        AdManagementValidationRequest request,
        CancellationToken cancellationToken = default);
}
