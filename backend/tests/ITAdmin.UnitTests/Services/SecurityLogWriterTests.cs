using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.TestInfrastructure;

namespace ITAdmin.UnitTests.Services;

public sealed class SecurityLogWriterTests
{
    [Fact]
    public async Task TryWriteAsync_TruncatesBoundedFieldsToConfiguredMaxLengths()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var writer = new SecurityLogWriter(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityLogWriter>.Instance);

        await writer.TryWriteAsync(
            new SecurityLogWriteRequest
            {
                EventType = new string('e', 150),
                Severity = new string('s', 40),
                UserName = new string('u', 120),
                IpAddress = new string('p', 80),
                UserAgent = new string('g', 1100),
                Description = new string('d', 2500),
            });

        var securityLog = Assert.Single(context.SecurityLogs);
        Assert.Equal(128, securityLog.EventType.Length);
        Assert.Equal(32, securityLog.Severity.Length);
        Assert.Equal(100, securityLog.UserName!.Length);
        Assert.Equal(64, securityLog.IpAddress!.Length);
        Assert.Equal(1024, securityLog.UserAgent!.Length);
        Assert.Equal(2000, securityLog.Description!.Length);
    }

    [Fact]
    public async Task TryWriteAsync_DoesNotThrow_WhenDatabaseWriteFails()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;
        await context.DisposeAsync();

        var writer = new SecurityLogWriter(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityLogWriter>.Instance);

        await writer.TryWriteAsync(
            new SecurityLogWriteRequest
            {
                EventType = "ForbiddenAccess",
                Description = "Access denied.",
            });
    }
}
