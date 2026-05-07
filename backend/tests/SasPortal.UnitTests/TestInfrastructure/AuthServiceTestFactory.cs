using Microsoft.Extensions.Options;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.Fakes;

namespace SasPortal.UnitTests.TestInfrastructure;

public static class AuthServiceTestFactory
{
    public static async Task<AuthServiceTestContext> CreateAsync()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        var ldapService = new FakeLdapService();
        var secretProtector = new FakeSecretProtector();
        var tokenService = new FakeTokenService();
        var authService = new AuthService(
            dbContext,
            ldapService,
            secretProtector,
            tokenService,
            Options.Create(new JwtOptions
            {
                Key = "unit-test-key-unit-test-key-unit-test-key",
                Issuer = "unit-tests",
                Audience = "unit-tests",
                AccessTokenMinutes = 30,
                RefreshTokenDays = 7
            }));

        return new AuthServiceTestContext(connection, dbContext, authService, ldapService, secretProtector, tokenService);
    }
}
