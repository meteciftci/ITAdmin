using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdComputerOuMoveService
{
    Task<MoveAdComputerOuResult> MoveOuAsync(
        MoveAdComputerOuRequest request,
        CancellationToken cancellationToken = default);
}
