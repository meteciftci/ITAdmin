using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAuthService
{
    Task<AuthTokenResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
