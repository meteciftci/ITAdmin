using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.UnitTests.Fakes;

public sealed class FakeTokenService : ITokenService
{
    private int _refreshTokenSequence;

    public string AccessTokenResult { get; set; } = "access-token";
    public string RefreshTokenResult { get; set; } = "refresh-token";
    public Exception? HashRefreshTokenException { get; set; }

    public string CreateAccessToken(AuthenticatedUserInfo user, DateTime expiresAt) => AccessTokenResult;

    public string CreateRefreshToken() => $"{RefreshTokenResult}-{_refreshTokenSequence++}";

    public string HashRefreshToken(string refreshToken)
    {
        if (HashRefreshTokenException is not null)
        {
            throw HashRefreshTokenException;
        }

        return $"hash:{refreshToken}";
    }
}
