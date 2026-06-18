using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdManagementValidationService
{
    Task<AdManagementValidationResult> ValidateConnectionAsync(
        AdManagementConnectionParameters connection,
        AdManagementValidationRequest request,
        CancellationToken cancellationToken = default);
}
