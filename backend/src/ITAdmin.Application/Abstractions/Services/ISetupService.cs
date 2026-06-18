using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface ISetupService
{
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);
    Task<CompleteSetupResult> CompleteSetupAsync(
        CompleteSetupRequest request,
        CancellationToken cancellationToken = default);
}
