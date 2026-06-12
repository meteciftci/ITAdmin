using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdComputerOuMoveService
{
    Task<MoveAdComputerOuResult> MoveOuAsync(
        MoveAdComputerOuRequest request,
        CancellationToken cancellationToken = default);
}
