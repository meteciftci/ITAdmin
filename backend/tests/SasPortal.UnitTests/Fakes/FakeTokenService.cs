using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.Fakes;

public sealed class FakeTokenService : ITokenService
{
    public string AccessTokenResult { get; set; } = "access-token";
    public string RefreshTokenResult { get; set; } = "refresh-token";
    public Exception? HashRefreshTokenException { get; set; }

    public string CreateAccessToken(AuthenticatedUserInfo user, DateTime expiresAt) => AccessTokenResult;

    public string CreateRefreshToken() => RefreshTokenResult;

    public string HashRefreshToken(string refreshToken)
    {
        if (HashRefreshTokenException is not null)
        {
            throw HashRefreshTokenException;
        }

        return $"hash:{refreshToken}";
    }
}
