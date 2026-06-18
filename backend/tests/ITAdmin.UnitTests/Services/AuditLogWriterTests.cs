using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.TestInfrastructure;

namespace ITAdmin.UnitTests.Services;

public sealed class AuditLogWriterTests
{
    [Fact]
    public async Task WriteAsync_TruncatesLongEntityIdTo128Characters()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var writer = new AuditLogWriter(context);
        var longEntityId = new string('e', 200);

        await writer.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = "Add",
                EntityName = "AdUserGroupMembership",
                EntityId = longEntityId,
                Description = "Test",
            });

        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal(128, audit.EntityId!.Length);
        Assert.Equal(longEntityId[..128], audit.EntityId);
    }

    [Fact]
    public async Task WriteAsync_TruncatesBoundedFieldsToConfiguredMaxLengths()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var writer = new AuditLogWriter(context);

        await writer.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = new string('a', 80),
                EntityName = new string('n', 150),
                EntityId = new string('i', 150),
                Description = new string('d', 2500),
                ActorUserName = new string('u', 120),
                IpAddress = new string('p', 80),
                UserAgent = new string('g', 1100),
            });

        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal(64, audit.Action.Length);
        Assert.Equal(128, audit.EntityName.Length);
        Assert.Equal(128, audit.EntityId!.Length);
        Assert.Equal(2000, audit.Description!.Length);
        Assert.Equal(100, audit.ActorUserName!.Length);
        Assert.Equal(64, audit.IpAddress!.Length);
        Assert.Equal(1024, audit.UserAgent!.Length);
    }
}
