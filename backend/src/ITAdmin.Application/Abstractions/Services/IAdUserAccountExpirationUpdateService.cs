using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdUserAccountExpirationUpdateService
{
    Task<UpdateAdUserAccountExpirationResult> UpdateAccountExpirationAsync(
        UpdateAdUserAccountExpirationRequest request,
        CancellationToken cancellationToken = default);
}
