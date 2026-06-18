using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdComputerDeleteService
{
    Task<DeleteAdComputerResult> DeleteComputerAsync(
        DeleteAdComputerRequest request,
        CancellationToken cancellationToken = default);
}
