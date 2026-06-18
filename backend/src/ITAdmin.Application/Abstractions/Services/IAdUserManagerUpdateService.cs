using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdUserManagerUpdateService
{
    Task<UpdateAdUserManagerResult> UpdateManagerAsync(
        UpdateAdUserManagerRequest request,
        CancellationToken cancellationToken = default);
}
