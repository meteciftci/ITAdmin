using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAuthService
{
    Task<AuthTokenResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokenResult> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<LogoutResult> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);

    Task<CurrentUserResult> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UpdateCurrentUserPreferencesResult> UpdateCurrentUserPreferencesAsync(
        UpdateCurrentUserPreferencesRequest request,
        CancellationToken cancellationToken = default);
}
