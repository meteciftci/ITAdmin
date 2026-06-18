using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Security;

public interface ITokenService
{
    string CreateAccessToken(AuthenticatedUserInfo user, DateTime expiresAt);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
