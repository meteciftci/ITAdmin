using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface ISetupService
{
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);
    Task<CompleteSetupResult> CompleteSetupAsync(
        CompleteSetupRequest request,
        CancellationToken cancellationToken = default);
}
