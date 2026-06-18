using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ITAdmin.Application.Common.Models;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.TestInfrastructure;

public static class AuthServiceTestFactory
{
    public static async Task<AuthServiceTestContext> CreateAsync()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        var ldapService = new FakeLdapService();
        var secretProtector = new FakeSecretProtector();
        var tokenService = new FakeTokenService();
        var settingsService = new AuthServiceSettingsFake();
        var authService = new AuthService(
            dbContext,
            ldapService,
            secretProtector,
            tokenService,
            settingsService,
            Options.Create(new JwtOptions
            {
                Key = "unit-test-key-unit-test-key-unit-test-key",
                Issuer = "unit-tests",
                Audience = "unit-tests",
                AccessTokenMinutes = 30,
                RefreshTokenDays = 7
            }),
            NullLogger<AuthService>.Instance);

        return new AuthServiceTestContext(connection, dbContext, authService, ldapService, secretProtector, tokenService, settingsService);
    }
}
