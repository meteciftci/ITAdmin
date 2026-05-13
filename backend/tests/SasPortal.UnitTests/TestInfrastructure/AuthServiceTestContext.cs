using Microsoft.Data.Sqlite;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.Fakes;

namespace SasPortal.UnitTests.TestInfrastructure;

public sealed class AuthServiceTestContext(
    SqliteConnection connection,
    AppDbContext dbContext,
    AuthService authService,
    FakeLdapService ldapService,
    FakeSecretProtector secretProtector,
    FakeTokenService tokenService,
    AuthServiceSettingsFake settingsService) : IAsyncDisposable
{
    public AppDbContext DbContext { get; } = dbContext;
    public AuthService AuthService { get; } = authService;
    public FakeLdapService LdapService { get; } = ldapService;
    public FakeSecretProtector SecretProtector { get; } = secretProtector;
    public FakeTokenService TokenService { get; } = tokenService;
    public AuthServiceSettingsFake SettingsService { get; } = settingsService;

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await connection.DisposeAsync();
    }
}
