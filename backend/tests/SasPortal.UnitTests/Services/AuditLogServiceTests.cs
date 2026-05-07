using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.Services;

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task GetAuditLogsAsync_DefaultsAndSorting()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedAuditLogsAsync(
            context,
            CreateAuditLog("UserUpdated", "PortalUser", "1", createdAt: Utc(2026, 1, 1, 10, 0)),
            CreateAuditLog("UserCreated", "PortalUser", "2", createdAt: Utc(2026, 1, 1, 12, 0)),
            CreateAuditLog("RoleUpdated", "PortalRole", "3", createdAt: Utc(2026, 1, 1, 11, 0)));

        var service = new AuditLogService(context);
        var result = await service.GetAuditLogsAsync(new AuditLogListQuery(null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(3, result.Items.Count);

        var createdAts = result.Items.Select(x => x.CreatedAt).ToArray();
        Assert.Equal(createdAts.OrderByDescending(x => x), createdAts);
    }

    [Fact]
    public async Task GetAuditLogsAsync_NormalizesInvalidPaging()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedAuditLogsAsync(context, CreateAuditLog("A", "E", "1", createdAt: Utc(2026, 1, 2, 10, 0)));
        var service = new AuditLogService(context);

        var low = await service.GetAuditLogsAsync(new AuditLogListQuery(null, null, null, null, null, null, null, null, 0, 0));
        Assert.Equal(1, low.PageNumber);
        Assert.Equal(20, low.PageSize);

        var high = await service.GetAuditLogsAsync(new AuditLogListQuery(null, null, null, null, null, null, null, null, 1, 200));
        Assert.Equal(100, high.PageSize);
    }

    [Fact]
    public async Task GetAuditLogsAsync_FiltersByActionsAndEntityNames()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedAuditLogsAsync(
            context,
            CreateAuditLog("UserCreated", "PortalUser", "1", createdAt: Utc(2026, 1, 3, 10, 0)),
            CreateAuditLog("UserUpdated", "PortalUser", "2", createdAt: Utc(2026, 1, 3, 11, 0)),
            CreateAuditLog("RoleCreated", "PortalRole", "3", createdAt: Utc(2026, 1, 3, 12, 0)));

        var query = new AuditLogListQuery(
            Search: null,
            Action: null,
            Actions: [" UserCreated ", "UserCreated", "UserUpdated", " "],
            EntityName: null,
            EntityNames: [" PortalUser ", "PortalUser", ""],
            ActorUserId: null,
            From: null,
            To: null,
            PageNumber: 1,
            PageSize: 20);

        var service = new AuditLogService(context);
        var result = await service.GetAuditLogsAsync(query);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, x => Assert.Equal("PortalUser", x.EntityName));
        Assert.All(result.Items, x => Assert.Contains(x.Action, new[] { "UserCreated", "UserUpdated" }));
    }

    [Fact]
    public async Task GetAuditLogsAsync_SupportsLegacySingleActionAndEntityName()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedAuditLogsAsync(
            context,
            CreateAuditLog("UserCreated", "PortalUser", "1", createdAt: Utc(2026, 1, 4, 9, 0)),
            CreateAuditLog("UserUpdated", "PortalUser", "2", createdAt: Utc(2026, 1, 4, 10, 0)),
            CreateAuditLog("UserCreated", "PortalRole", "3", createdAt: Utc(2026, 1, 4, 11, 0)));

        var query = new AuditLogListQuery(
            Search: null,
            Action: " UserCreated ",
            Actions: [],
            EntityName: " PortalUser ",
            EntityNames: null,
            ActorUserId: null,
            From: null,
            To: null,
            PageNumber: 1,
            PageSize: 20);

        var service = new AuditLogService(context);
        var result = await service.GetAuditLogsAsync(query);

        var item = Assert.Single(result.Items);
        Assert.Equal("UserCreated", item.Action);
        Assert.Equal("PortalUser", item.EntityName);
    }

    [Fact]
    public async Task GetAuditLogsAsync_FiltersByActorUserId()
    {
        var actorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var otherId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedAuditLogsAsync(
            context,
            CreateAuditLog("UserCreated", "PortalUser", "1", actorUserId: actorId, createdAt: Utc(2026, 1, 5, 10, 0)),
            CreateAuditLog("RoleCreated", "PortalRole", "2", actorUserId: otherId, createdAt: Utc(2026, 1, 5, 11, 0)));

        var service = new AuditLogService(context);
        var result = await service.GetAuditLogsAsync(new AuditLogListQuery(null, null, null, null, null, actorId, null, null, 1, 20));

        var item = Assert.Single(result.Items);
        Assert.Equal(actorId, item.ActorUserId);
    }

    [Fact]
    public async Task GetAuditLogsAsync_FiltersByDateRangeWithTimezone()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var fromUtc = Utc(2026, 2, 1, 10, 0);
        var toUtc = Utc(2026, 2, 1, 12, 0);

        await SeedAuditLogsAsync(
            context,
            CreateAuditLog("Before", "PortalUser", "0", createdAt: Utc(2026, 2, 1, 9, 59)),
            CreateAuditLog("FromBoundary", "PortalUser", "1", createdAt: fromUtc),
            CreateAuditLog("Inside", "PortalUser", "2", createdAt: Utc(2026, 2, 1, 11, 0)),
            CreateAuditLog("ToBoundary", "PortalUser", "3", createdAt: toUtc),
            CreateAuditLog("After", "PortalUser", "4", createdAt: Utc(2026, 2, 1, 12, 1)));

        var query = new AuditLogListQuery(
            Search: null,
            Action: null,
            Actions: null,
            EntityName: null,
            EntityNames: null,
            ActorUserId: null,
            From: new DateTimeOffset(2026, 2, 1, 13, 0, 0, TimeSpan.FromHours(3)),
            To: new DateTimeOffset(2026, 2, 1, 14, 0, 0, TimeSpan.FromHours(2)),
            PageNumber: 1,
            PageSize: 20);

        var service = new AuditLogService(context);
        var result = await service.GetAuditLogsAsync(query);

        Assert.Equal(3, result.TotalCount);
        Assert.DoesNotContain(result.Items, x => x.Action is "Before" or "After");
        Assert.Contains(result.Items, x => x.Action == "FromBoundary");
        Assert.Contains(result.Items, x => x.Action == "ToBoundary");
    }

    [Fact]
    public async Task GetAuditLogsAsync_SearchesExpectedFields()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedAuditLogsAsync(
            context,
            CreateAuditLog("UserCreated", "PortalUser", "entity-10", description: "Primary user changed", actorUserName: "MeteAdmin", createdAt: Utc(2026, 1, 6, 10, 0)),
            CreateAuditLog("RoleCreated", "PortalRole", "role-20", description: "No match", actorUserName: "Operator", createdAt: Utc(2026, 1, 6, 11, 0)));

        var service = new AuditLogService(context);

        var byActor = await service.GetAuditLogsAsync(new AuditLogListQuery("portaluser", null, null, null, null, null, null, null, 1, 20));
        Assert.Single(byActor.Items);

        var byActionCase = await service.GetAuditLogsAsync(new AuditLogListQuery("usercreated", null, null, null, null, null, null, null, 1, 20));
        Assert.Single(byActionCase.Items);

        var byEntityNameCase = await service.GetAuditLogsAsync(new AuditLogListQuery("PORTALUSER", null, null, null, null, null, null, null, 1, 20));
        Assert.Single(byEntityNameCase.Items);

        var byEntityId = await service.GetAuditLogsAsync(new AuditLogListQuery("ENTITY-10", null, null, null, null, null, null, null, 1, 20));
        Assert.Single(byEntityId.Items);

        var byDescription = await service.GetAuditLogsAsync(new AuditLogListQuery("primary USER", null, null, null, null, null, null, null, 1, 20));
        Assert.Single(byDescription.Items);
    }

    [Fact]
    public async Task GetAuditLogsAsync_Paginates()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var logs = Enumerable.Range(1, 25)
            .Select(i => CreateAuditLog("Action", "Entity", i.ToString(), createdAt: Utc(2026, 1, 7, 0, i)))
            .ToArray();
        await SeedAuditLogsAsync(context, logs);

        var service = new AuditLogService(context);
        var result = await service.GetAuditLogsAsync(new AuditLogListQuery(null, null, null, null, null, null, null, null, 2, 10));

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

        await SeedAuditLogsAsync(
            context,
            CreateAuditLog(" UserCreated ", " PortalUser ", "1", createdAt: Utc(2026, 1, 8, 10, 0)),
            CreateAuditLog("UserCreated", "PortalUser", "2", createdAt: Utc(2026, 1, 8, 11, 0)),
            CreateAuditLog("RoleUpdated", "PortalRole", "3", createdAt: Utc(2026, 1, 8, 12, 0)),
            CreateAuditLog(" ", " ", "4", createdAt: Utc(2026, 1, 8, 13, 0)),
            CreateAuditLog("RoleUpdated", "PortalRole", "5", createdAt: Utc(2026, 1, 8, 14, 0)));

        var service = new AuditLogService(context);
        var options = await service.GetFilterOptionsAsync();

        Assert.Equal(new[] { "RoleUpdated", "UserCreated" }, options.Actions);
        Assert.Equal(new[] { "PortalRole", "PortalUser" }, options.EntityNames);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static AuditLog CreateAuditLog(
        string action,
        string entityName,
        string entityId,
        string? description = null,
        Guid? actorUserId = null,
        string? actorUserName = "portalUser",
        DateTimeOffset? createdAt = null)
        => new()
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            ActorUserId = actorUserId,
            ActorUserName = actorUserName,
            IpAddress = "127.0.0.1",
            UserAgent = "xunit",
            CreatedAt = createdAt ?? Utc(2026, 1, 1, 0, 0)
        };

    private static async Task SeedAuditLogsAsync(
        SasPortal.Persistence.Context.AppDbContext context,
        params AuditLog[] logs)
    {
        await context.AuditLogs.AddRangeAsync(logs);
        await context.SaveChangesAsync();
    }
}
