using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdUserManagerUpdateService
{
    Task<UpdateAdUserManagerResult> UpdateManagerAsync(
        UpdateAdUserManagerRequest request,
        CancellationToken cancellationToken = default);
}
