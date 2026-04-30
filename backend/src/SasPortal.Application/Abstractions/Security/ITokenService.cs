using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Security;

public interface ITokenService
{
    string CreateAccessToken(AuthenticatedUserInfo user, DateTime expiresAt);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
