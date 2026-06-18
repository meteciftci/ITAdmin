using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdUserOuMoveService
{
    Task<MoveAdUserOuResult> MoveOuAsync(
        MoveAdUserOuRequest request,
        CancellationToken cancellationToken = default);
}
