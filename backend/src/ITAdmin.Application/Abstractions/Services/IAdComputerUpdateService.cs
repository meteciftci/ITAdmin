using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdComputerUpdateService
{
    Task<UpdateAdComputerResult> UpdateComputerAsync(
        UpdateAdComputerRequest request,
        CancellationToken cancellationToken = default);
}
