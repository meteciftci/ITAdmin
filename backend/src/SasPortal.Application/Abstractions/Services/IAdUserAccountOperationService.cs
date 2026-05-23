using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdUserAccountOperationService
{
    Task<AdUserAccountOperationResult> EnableAsync(
        AdUserAccountOperationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdUserAccountOperationResult> DisableAsync(
        AdUserAccountOperationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdUserAccountOperationResult> UnlockAsync(
        AdUserAccountOperationRequest request,
        CancellationToken cancellationToken = default);
}
