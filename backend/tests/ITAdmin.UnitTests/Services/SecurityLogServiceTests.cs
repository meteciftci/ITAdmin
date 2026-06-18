using ITAdmin.Application.Common.Models;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.TestInfrastructure;

namespace ITAdmin.UnitTests.Services;

public sealed class SecurityLogServiceTests
{
    [Fact]
    public async Task GetSecurityLogsAsync_DefaultsAndSorting()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedSecurityLogsAsync(
            context,
            CreateSecurityLog("LoginSuccess", "Info", createdAt: Utc(2026, 3, 1, 10, 0)),
            CreateSecurityLog("LoginFailed", "Warning", createdAt: Utc(2026, 3, 1, 12, 0)),
            CreateSecurityLog("ForbiddenAccess", "Critical", createdAt: Utc(2026, 3, 1, 11, 0)));

        var service = new SecurityLogService(context);
        var result = await service.GetSecurityLogsAsync(new SecurityLogListQuery(null, null, null, null, null, null, 1, 20));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);

        var createdAts = result.Items.Select(x => x.CreatedAt).ToArray();
        Assert.Equal(createdAts.OrderByDescending(x => x), createdAts);
    }

    [Fact]
    public async Task GetSecurityLogsAsync_NormalizesInvalidPaging()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedSecurityLogsAsync(context, CreateSecurityLog("LoginSuccess", "Info", createdAt: Utc(2026, 3, 2, 10, 0)));
        var service = new SecurityLogService(context);

        var low = await service.GetSecurityLogsAsync(new SecurityLogListQuery(null, null, null, null, null, null, 0, 0));
        Assert.Equal(1, low.PageNumber);
        Assert.Equal(20, low.PageSize);

        var high = await service.GetSecurityLogsAsync(new SecurityLogListQuery(null, null, null, null, null, null, 1, 500));
        Assert.Equal(100, high.PageSize);
    }

    [Fact]
    public async Task GetSecurityLogsAsync_FiltersByEventTypesAndSeverities()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedSecurityLogsAsync(
            context,
            CreateSecurityLog("LoginSuccess", "Info", createdAt: Utc(2026, 3, 3, 10, 0)),
            CreateSecurityLog("LoginFailed", "Warning", createdAt: Utc(2026, 3, 3, 11, 0)),
            CreateSecurityLog("ForbiddenAccess", "Critical", createdAt: Utc(2026, 3, 3, 12, 0)));

        var query = new SecurityLogListQuery(
            Search: null,
            EventTypes: [" LoginSuccess ", "LoginSuccess", "LoginFailed", " "],
            Severities: [" Info ", "Warning", "Warning"],
            UserId: null,
            From: null,
            To: null,
            PageNumber: 1,
            PageSize: 20);

        var service = new SecurityLogService(context);
        var result = await service.GetSecurityLogsAsync(query);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, x => Assert.Contains(x.EventType, new[] { "LoginSuccess", "LoginFailed" }));
        Assert.All(result.Items, x => Assert.Contains(x.Severity, new[] { "Info", "Warning" }));
    }

    [Fact]
    public async Task GetSecurityLogsAsync_FiltersByUserId()
    {
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var otherUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedSecurityLogsAsync(
            context,
            CreateSecurityLog("LoginSuccess", "Info", userId: userId, createdAt: Utc(2026, 3, 4, 10, 0)),
            CreateSecurityLog("LoginFailed", "Warning", userId: otherUserId, createdAt: Utc(2026, 3, 4, 11, 0)));

        var service = new SecurityLogService(context);
        var result = await service.GetSecurityLogsAsync(new SecurityLogListQuery(null, null, null, userId, null, null, 1, 20));

        var item = Assert.Single(result.Items);
        Assert.Equal(userId, item.UserId);
    }

    [Fact]
    public async Task GetSecurityLogsAsync_FiltersByDateRangeWithTimezone()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var fromUtc = Utc(2026, 3, 5, 10, 0);
        var toUtc = Utc(2026, 3, 5, 12, 0);

        await SeedSecurityLogsAsync(
            context,
            CreateSecurityLog("Before", "Info", createdAt: Utc(2026, 3, 5, 9, 59)),
            CreateSecurityLog("FromBoundary", "Info", createdAt: fromUtc),
            CreateSecurityLog("Inside", "Warning", createdAt: Utc(2026, 3, 5, 11, 0)),
            CreateSecurityLog("ToBoundary", "Critical", createdAt: toUtc),
            CreateSecurityLog("After", "Info", createdAt: Utc(2026, 3, 5, 12, 1)));

        var query = new SecurityLogListQuery(
            Search: null,
            EventTypes: null,
            Severities: null,
            UserId: null,
            From: new DateTimeOffset(2026, 3, 5, 13, 0, 0, TimeSpan.FromHours(3)),
            To: new DateTimeOffset(2026, 3, 5, 14, 0, 0, TimeSpan.FromHours(2)),
            PageNumber: 1,
            PageSize: 20);

        var service = new SecurityLogService(context);
        var result = await service.GetSecurityLogsAsync(query);

        Assert.Equal(3, result.TotalCount);
        Assert.DoesNotContain(result.Items, x => x.EventType is "Before" or "After");
        Assert.Contains(result.Items, x => x.EventType == "FromBoundary");
        Assert.Contains(result.Items, x => x.EventType == "ToBoundary");
    }

    [Fact]
    public async Task GetSecurityLogsAsync_SearchesExpectedFields()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedSecurityLogsAsync(
            context,
            CreateSecurityLog("LoginSuccess", "Info", userName: "MeteUser", ipAddress: "10.10.10.10", description: "Portal access success", createdAt: Utc(2026, 3, 6, 10, 0)),
            CreateSecurityLog("RefreshTokenFailed", "Warning", userName: "Operator", ipAddress: "10.10.10.11", description: "No match", createdAt: Utc(2026, 3, 6, 11, 0)));

        var service = new SecurityLogService(context);

        var byEventType = await service.GetSecurityLogsAsync(new SecurityLogListQuery("loginsuccess", null, null, null, null, null, 1, 20));
        Assert.Single(byEventType.Items);

        var bySeverity = await service.GetSecurityLogsAsync(new SecurityLogListQuery("INFO", null, null, null, null, null, 1, 20));
        Assert.Single(bySeverity.Items);

        var byUserName = await service.GetSecurityLogsAsync(new SecurityLogListQuery("mete", null, null, null, null, null, 1, 20));
        Assert.Single(byUserName.Items);

        var byIp = await service.GetSecurityLogsAsync(new SecurityLogListQuery("10.10.10.10", null, null, null, null, null, 1, 20));
        Assert.Single(byIp.Items);

        var byDescription = await service.GetSecurityLogsAsync(new SecurityLogListQuery("PORTAL ACCESS", null, null, null, null, null, 1, 20));
        Assert.Single(byDescription.Items);
    }

    [Fact]
    public async Task GetSecurityLogsAsync_Paginates()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var logs = Enumerable.Range(1, 25)
            .Select(i => CreateSecurityLog("LoginSuccess", "Info", createdAt: Utc(2026, 3, 7, 0, i)))
            .ToArray();
        await SeedSecurityLogsAsync(context, logs);

        var service = new SecurityLogService(context);
        var result = await service.GetSecurityLogsAsync(new SecurityLogListQuery(null, null, null, null, null, null, 2, 10));

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetFilterOptionsAsync_ReturnsDistinctSortedTrimmedValues()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedSecurityLogsAsync(
            context,
            CreateSecurityLog(" LoginSuccess ", " Info ", createdAt: Utc(2026, 3, 8, 10, 0)),
            CreateSecurityLog("LoginSuccess", "Info", createdAt: Utc(2026, 3, 8, 11, 0)),
            CreateSecurityLog("ForbiddenAccess", "Critical", createdAt: Utc(2026, 3, 8, 12, 0)),
            CreateSecurityLog(" ", " ", createdAt: Utc(2026, 3, 8, 13, 0)));

        var service = new SecurityLogService(context);
        var options = await service.GetFilterOptionsAsync();

        Assert.Equal(new[] { "ForbiddenAccess", "LoginSuccess" }, options.EventTypes);
        Assert.Equal(new[] { "Critical", "Info" }, options.Severities);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static SecurityLog CreateSecurityLog(
        string eventType,
        string severity,
        Guid? userId = null,
        string? userName = "portalUser",
        string? ipAddress = "127.0.0.1",
        string? description = null,
        DateTimeOffset? createdAt = null)
        => new()
        {
            EventType = eventType,
            Severity = severity,
            UserId = userId,
            UserName = userName,
            IpAddress = ipAddress,
            UserAgent = "xunit",
            Description = description,
            CreatedAt = createdAt ?? Utc(2026, 3, 1, 0, 0)
        };

    private static async Task SeedSecurityLogsAsync(
        ITAdmin.Persistence.Context.AppDbContext context,
        params SecurityLog[] logs)
    {
        await context.SecurityLogs.AddRangeAsync(logs);
        await context.SaveChangesAsync();
    }
}
