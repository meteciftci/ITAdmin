using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdUserAccountExpirationUpdateService
{
    Task<UpdateAdUserAccountExpirationResult> UpdateAccountExpirationAsync(
        UpdateAdUserAccountExpirationRequest request,
        CancellationToken cancellationToken = default);
}
