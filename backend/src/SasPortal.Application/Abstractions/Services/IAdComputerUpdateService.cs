using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdComputerUpdateService
{
    Task<UpdateAdComputerResult> UpdateComputerAsync(
        UpdateAdComputerRequest request,
        CancellationToken cancellationToken = default);
}
