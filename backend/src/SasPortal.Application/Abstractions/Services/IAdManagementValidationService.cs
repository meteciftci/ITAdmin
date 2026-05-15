using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdManagementValidationService
{
    Task<AdManagementValidationResult> ValidateConnectionAsync(
        AdManagementConnectionParameters connection,
        AdManagementValidationRequest request,
        CancellationToken cancellationToken = default);
}
