using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdComputerDeleteService
{
    Task<DeleteAdComputerResult> DeleteComputerAsync(
        DeleteAdComputerRequest request,
        CancellationToken cancellationToken = default);
}
