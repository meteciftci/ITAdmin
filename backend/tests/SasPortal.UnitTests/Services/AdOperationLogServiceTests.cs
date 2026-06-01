using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.Services;

public sealed class AdOperationLogServiceTests
{
    [Fact]
    public async Task GetLogsAsync_DefaultsAndSorting()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedLogsAsync(
            context,
            CreateLog("UserUpdate", createdAt: Utc(2026, 1, 1, 10, 0)),
            CreateLog("UserEnable", createdAt: Utc(2026, 1, 1, 12, 0)),
            CreateLog("UserDisable", createdAt: Utc(2026, 1, 1, 11, 0)));

        var service = new AdOperationLogService(context);
        var result = await service.GetLogsAsync(
            new AdOperationLogListQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("UserEnable", result.Items.ElementAt(0).OperationType);
        Assert.Equal("UserDisable", result.Items.ElementAt(1).OperationType);
        Assert.Equal("UserUpdate", result.Items.ElementAt(2).OperationType);
    }

    [Fact]
    public async Task GetLogsAsync_NormalizesInvalidPaging()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedLogsAsync(context, CreateLog("UserUpdate", createdAt: Utc(2026, 1, 2, 10, 0)));
        var service = new AdOperationLogService(context);

        var low = await service.GetLogsAsync(
            new AdOperationLogListQuery(null, null, null, null, null, null, null, null, null, 0, 0));
        Assert.Equal(1, low.PageNumber);
        Assert.Equal(20, low.PageSize);

        var high = await service.GetLogsAsync(
            new AdOperationLogListQuery(null, null, null, null, null, null, null, null, null, 1, 200));
        Assert.Equal(100, high.PageSize);
    }

    [Fact]
    public async Task GetLogsAsync_FiltersByStatusAndOperationType()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedLogsAsync(
            context,
            CreateLog("UserUpdate", status: AdManagementOperationStatuses.Failed, samAccountName: "user.a"),
            CreateLog("UserEnable", status: AdManagementOperationStatuses.Succeeded, samAccountName: "user.b"),
            CreateLog("UserUpdate", status: AdManagementOperationStatuses.Succeeded, samAccountName: "user.c"));

        var service = new AdOperationLogService(context);
        var result = await service.GetLogsAsync(
            new AdOperationLogListQuery(
                "UserUpdate",
                AdManagementOperationStatuses.Failed,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                20));

        Assert.Single(result.Items);
        var item = result.Items.Single();
        Assert.Equal("user.a", item.TargetSamAccountName);
        Assert.True(item.HasError);
    }

    [Fact]
    public async Task GetLogsAsync_FiltersByTargetSearch()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedLogsAsync(
            context,
            CreateLog("UserUpdate", samAccountName: "alpha.user"),
            CreateLog("UserUpdate", objectGuid: "bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));

        var service = new AdOperationLogService(context);

        var bySam = await service.GetLogsAsync(
            new AdOperationLogListQuery(null, null, null, "alpha", null, null, null, null, null, 1, 20));
        Assert.Single(bySam.Items);

        var byGuid = await service.GetLogsAsync(
            new AdOperationLogListQuery(null, null, null, "bbbb", null, null, null, null, null, 1, 20));
        Assert.Single(byGuid.Items);
    }

    [Fact]
    public async Task GetLogsAsync_PreservesJsonDiagnosticErrorMessage()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        const string diagnosticJson =
            """{"code":"UpdateFailed","operation":"UserUpdate","message":"Test failure"}""";

        await SeedLogsAsync(
            context,
            CreateLog(
                "UserUpdate",
                status: AdManagementOperationStatuses.Failed,
                errorMessage: diagnosticJson));

        var service = new AdOperationLogService(context);
        var result = await service.GetLogsAsync(
            new AdOperationLogListQuery(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.Equal(diagnosticJson, Assert.Single(result.Items).ErrorMessage);
    }

    [Fact]
    public async Task WriteAsync_TruncatesLongTargetDistinguishedNameTo1000Characters()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var service = new AdOperationLogService(context);
        var longDn = "CN=" + new string('x', 1200) + ",OU=Groups,DC=example,DC=com";

        await service.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.UserGroupAdd,
                Status = AdManagementOperationStatuses.Succeeded,
                TargetDistinguishedName = longDn,
            });

        await context.SaveChangesAsync();

        var log = Assert.Single(context.AdOperationLogs);
        Assert.Equal(1000, log.TargetDistinguishedName!.Length);
        Assert.Equal(longDn[..1000], log.TargetDistinguishedName);
    }

    [Fact]
    public async Task WriteAsync_TruncatesBoundedFieldsToConfiguredMaxLengths()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var service = new AdOperationLogService(context);

        await service.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = new string('o', 80),
                Status = new string('s', 50),
                TargetObjectType = new string('t', 80),
                TargetDistinguishedName = new string('d', 1100),
                TargetObjectGuid = new string('g', 80),
                TargetSamAccountName = new string('a', 120),
                ErrorCode = new string('c', 80),
                ErrorMessage = new string('m', 2500),
                DomainController = new string('h', 300),
                ActorUserName = new string('u', 120),
                IpAddress = new string('p', 80),
                UserAgent = new string('v', 1100),
                CorrelationId = new string('r', 80),
            });

        await context.SaveChangesAsync();

        var log = Assert.Single(context.AdOperationLogs);
        Assert.Equal(64, log.OperationType.Length);
        Assert.Equal(32, log.Status.Length);
        Assert.Equal(64, log.TargetObjectType!.Length);
        Assert.Equal(1000, log.TargetDistinguishedName!.Length);
        Assert.Equal(64, log.TargetObjectGuid!.Length);
        Assert.Equal(100, log.TargetSamAccountName!.Length);
        Assert.Equal(64, log.ErrorCode!.Length);
        Assert.Equal(2000, log.ErrorMessage!.Length);
        Assert.Equal(250, log.DomainController!.Length);
        Assert.Equal(100, log.ActorUserName!.Length);
        Assert.Equal(64, log.IpAddress!.Length);
        Assert.Equal(1024, log.UserAgent!.Length);
        Assert.Equal(64, log.CorrelationId!.Length);
    }

    [Fact]
    public async Task GetLogByIdAsync_ReturnsDetailOrNull()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var log = CreateLog(
            "UserUpdate",
            requestSummaryJson: """{"changeStatus":"NoChangesDetected"}""",
            beforeSnapshotJson: """{"samAccountName":"test"}""");
        await SeedLogsAsync(context, log);

        var service = new AdOperationLogService(context);

        var detail = await service.GetLogByIdAsync(log.Id);
        Assert.NotNull(detail);
        Assert.Equal(log.Id, detail!.Id);
        Assert.Equal("""{"changeStatus":"NoChangesDetected"}""", detail.RequestSummaryJson);
        Assert.Equal("""{"samAccountName":"test"}""", detail.BeforeSnapshotJson);

        var missing = await service.GetLogByIdAsync(Guid.NewGuid());
        Assert.Null(missing);
    }

    private static async Task SeedLogsAsync(AppDbContext context, params AdOperationLog[] logs)
    {
        context.AdOperationLogs.AddRange(logs);
        await context.SaveChangesAsync();
    }

    private static AdOperationLog CreateLog(
        string operationType,
        string status = AdManagementOperationStatuses.Succeeded,
        string? samAccountName = null,
        string? objectGuid = null,
        string? errorMessage = null,
        string? requestSummaryJson = null,
        string? beforeSnapshotJson = null,
        DateTimeOffset? createdAt = null)
    {
        return new AdOperationLog
        {
            OperationType = operationType,
            Status = status,
            TargetSamAccountName = samAccountName,
            TargetObjectGuid = objectGuid,
            ErrorMessage = errorMessage,
            RequestSummaryJson = requestSummaryJson,
            BeforeSnapshotJson = beforeSnapshotJson,
            CreatedAt = createdAt ?? Utc(2026, 1, 1, 0, 0),
        };
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
