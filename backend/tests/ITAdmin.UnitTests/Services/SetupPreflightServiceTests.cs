using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Security;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.Services;

public sealed class SetupPreflightServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenSetupRequired_ReturnsChecksWithoutSecretValues()
    {
        await using var context = CreateDbContext();
        var keysDirectory = CreateWritableDirectory();
        var configuration = BuildConfiguration(
            jwtKey: "test-jwt-key-with-at-least-32-characters",
            setupKeyHash: SetupKeyHashValidator.ComputeConfiguredHash("setup-secret"),
            dataProtectionKeysPath: keysDirectory);

        var service = CreateService(context, configuration, new FakeHostEnvironment
        {
            EnvironmentName = Environments.Production,
            ApplicationName = "ITAdmin.Api",
        });

        var result = await service.CheckAsync();

        Assert.NotEmpty(result.Checks);
        Assert.Contains(result.Checks, check => check.Key == SetupPreflightCheckKeys.JwtKeyConfigured);
        Assert.Contains(result.Checks, check => check.Key == SetupPreflightCheckKeys.SetupKeyHashConfigured);
        Assert.DoesNotContain(result.Checks, check => check.Detail?.Contains("test-jwt-key", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Checks, check => check.Detail?.Contains("setup-secret", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Checks, check => check.Detail?.Contains("sha256:", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task CheckAsync_WhenDataProtectionKeysPathMissing_ReportsErrorInProduction()
    {
        await using var context = CreateDbContext();
        var configuration = BuildConfiguration(
            jwtKey: "test-jwt-key-with-at-least-32-characters",
            setupKeyHash: SetupKeyHashValidator.ComputeConfiguredHash("setup-secret"),
            dataProtectionKeysPath: "C:\\missing\\itadmin-keys");

        var service = CreateService(context, configuration, new FakeHostEnvironment
        {
            EnvironmentName = Environments.Production,
        });

        var result = await service.CheckAsync();
        var existsCheck = Assert.Single(result.Checks, check => check.Key == SetupPreflightCheckKeys.DataProtectionKeysPathExists);

        Assert.Equal(SetupPreflightCheckStatuses.Error, existsCheck.Status);
        Assert.Equal(SetupPreflightMessageKeys.DataProtectionKeysPathMissingOnDisk, existsCheck.MessageKey);
    }

    [Fact]
    public async Task CheckAsync_WhenDataProtectionKeysPathMissing_ReportsWarningInDevelopment()
    {
        await using var context = CreateDbContext();
        var configuration = BuildConfiguration(
            jwtKey: "test-jwt-key-with-at-least-32-characters",
            setupKeyHash: SetupKeyHashValidator.ComputeConfiguredHash("setup-secret"),
            dataProtectionKeysPath: "C:\\missing\\itadmin-keys");

        var service = CreateService(context, configuration, new FakeHostEnvironment
        {
            EnvironmentName = Environments.Development,
        });

        var result = await service.CheckAsync();
        var existsCheck = Assert.Single(result.Checks, check => check.Key == SetupPreflightCheckKeys.DataProtectionKeysPathExists);

        Assert.Equal(SetupPreflightCheckStatuses.Warning, existsCheck.Status);
    }

    [Fact]
    public async Task CheckAsync_WhenDataProtectionKeysPathNotWritable_ReportsErrorInProduction()
    {
        await using var context = CreateDbContext();
        var nonWritableExistingPath = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.Windows)
            : "/usr/bin";
        var configuration = BuildConfiguration(
            jwtKey: "test-jwt-key-with-at-least-32-characters",
            setupKeyHash: SetupKeyHashValidator.ComputeConfiguredHash("setup-secret"),
            dataProtectionKeysPath: nonWritableExistingPath);

        var service = CreateService(context, configuration, new FakeHostEnvironment
        {
            EnvironmentName = Environments.Production,
        });

        var result = await service.CheckAsync();
        var writableCheck = Assert.Single(result.Checks, check => check.Key == SetupPreflightCheckKeys.DataProtectionKeysPathWritable);

        Assert.Equal(SetupPreflightCheckStatuses.Error, writableCheck.Status);
        Assert.Equal(SetupPreflightMessageKeys.DataProtectionKeysPathNotWritable, writableCheck.MessageKey);
    }

    [Fact]
    public async Task CheckAsync_WhenSetupKeyHashFormatInvalid_ReportsError()
    {
        await using var context = CreateDbContext();
        var configuration = BuildConfiguration(
            jwtKey: "test-jwt-key-with-at-least-32-characters",
            setupKeyHash: "invalid-hash-format",
            dataProtectionKeysPath: CreateWritableDirectory());

        var service = CreateService(context, configuration, new FakeHostEnvironment());

        var result = await service.CheckAsync();
        var formatCheck = Assert.Single(result.Checks, check => check.Key == SetupPreflightCheckKeys.SetupKeyHashValidFormat);

        Assert.Equal(SetupPreflightCheckStatuses.Error, formatCheck.Status);
        Assert.Equal(SetupPreflightMessageKeys.SetupKeyHashInvalidFormat, formatCheck.MessageKey);
    }

    private static SetupPreflightService CreateService(
        AppDbContext context,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment) =>
        new(context, configuration, hostEnvironment, new SetupKeyHashValidator());

    private static IConfiguration BuildConfiguration(
        string jwtKey,
        string setupKeyHash,
        string dataProtectionKeysPath) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = jwtKey,
                ["Jwt:Issuer"] = "ITAdmin",
                ["Jwt:Audience"] = "ITAdmin.Client",
                [SetupKeyHashValidator.ConfigurationKey] = setupKeyHash,
                ["DataProtection:ApplicationName"] = "ITAdmin-Production",
                ["DataProtection:KeysPath"] = dataProtectionKeysPath,
            })
            .Build();

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static string CreateWritableDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "itadmin-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
