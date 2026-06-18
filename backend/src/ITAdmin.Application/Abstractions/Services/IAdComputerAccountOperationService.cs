using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdComputerAccountOperationService
{
    Task<AdComputerAccountOperationResult> EnableComputerAsync(
        AdComputerAccountOperationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdComputerAccountOperationResult> DisableComputerAsync(
        AdComputerAccountOperationRequest request,
        CancellationToken cancellationToken = default);
}
