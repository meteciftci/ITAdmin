using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdUserOuMoveService
{
    Task<MoveAdUserOuResult> MoveOuAsync(
        MoveAdUserOuRequest request,
        CancellationToken cancellationToken = default);
}
