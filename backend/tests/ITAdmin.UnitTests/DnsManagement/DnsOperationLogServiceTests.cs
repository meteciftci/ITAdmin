using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.UnitTests.DnsManagement;

public sealed class DnsOperationLogServiceTests
{
    [Fact]
    public void Endpoints_require_dns_operation_log_permission()
    {
        AssertPermission(nameof(DnsOperationLogsController.Get));
        AssertPermission(nameof(DnsOperationLogsController.GetById));
    }

    [Fact]
    public async Task List_filters_target_actor_and_date_and_returns_snapshot_flags()
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        context.DnsOperationLogs.AddRange(
            Log("RecordUpdate", "Succeeded", now, "Internal DNS", "www.example.test", "admin", "{}"),
            Log("ZoneDelete", "Failed", now.AddDays(-2), "Public DNS", "old.example.test", "operator", null));
        await context.SaveChangesAsync();

        var result = await new DnsOperationLogService(context).GetAsync(new(
            null, "RecordUpdate", "Succeeded", "WWW.EXAMPLE", "ADM",
            now.AddHours(-1), now.AddHours(1), 1, 20));

        var item = Assert.Single(result.Items);
        Assert.Equal("Internal DNS", item.ServerDisplayName);
        Assert.True(item.HasRequestSummary);
        Assert.True(item.HasBeforeSnapshot);
        Assert.True(item.HasAfterSnapshot);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Detail_returns_diagnostic_and_actor_fields()
    {
        await using var context = CreateContext();
        var entity = Log("CacheClear", "Failed", DateTimeOffset.UtcNow,
            "Internal DNS", null, "admin", null);
        entity.ErrorCode = "Timeout";
        entity.ErrorMessage = "Timed out.";
        entity.CorrelationId = "correlation";
        context.Add(entity);
        await context.SaveChangesAsync();

        var result = await new DnsOperationLogService(context).GetByIdAsync(entity.Id);

        Assert.NotNull(result);
        Assert.Equal("Timeout", result.ErrorCode);
        Assert.Equal("correlation", result.CorrelationId);
        Assert.Equal("admin", result.ActorUserName);
    }

    private static void AssertPermission(string method)
    {
        var attribute = typeof(DnsOperationLogsController).GetMethod(method)!
            .GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal($"Permission:{DnsManagementPermissions.ViewOperationLogs}", attribute?.Policy);
    }

    private static DnsOperationLog Log(
        string operation, string status, DateTimeOffset createdAt,
        string server, string? record, string actor, string? summary) => new()
        {
            OperationType = operation,
            Status = status,
            CreatedAt = createdAt,
            ServerDisplayName = server,
            ZoneName = record is null ? null : "example.test",
            RecordName = record,
            RecordType = record is null ? null : "A",
            ActorUserName = actor,
            RequestSummaryJson = summary,
            BeforeSnapshotJson = "{}",
            AfterSnapshotJson = "{}",
        };

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
}
