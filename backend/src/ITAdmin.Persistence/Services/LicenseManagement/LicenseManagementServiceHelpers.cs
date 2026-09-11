using ITAdmin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services.LicenseManagement;

internal static class LicenseManagementServiceHelpers
{
    internal const int AuditDescriptionMaxLength = 2000;
    internal const int AuditIpAddressMaxLength = 64;
    internal const int AuditUserAgentMaxLength = 1024;

    internal static (int PageNumber, int PageSize) NormalizePaging(int pageNumber, int pageSize) =>
        (
            pageNumber < 1 ? 1 : pageNumber,
            pageSize switch
            {
                < 1 => 20,
                > 100 => 100,
                _ => pageSize
            });

    internal static string BuildILikeContainsPattern(string search) =>
        $"%{search.Trim().Replace("%", "\\%").Replace("_", "\\_")}%";

    internal static bool UsesPostgreSql(AppDbContext context) =>
        string.Equals(
            context.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);

    internal static async Task<IDbContextTransaction?> BeginMutationTransactionAsync(
        AppDbContext context,
        CancellationToken cancellationToken) =>
        context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

    internal static async Task LockLicensePackagesAsync(
        AppDbContext context,
        IEnumerable<Guid> packageIds,
        CancellationToken cancellationToken)
    {
        var ids = packageIds.Distinct().OrderBy(x => x).ToArray();
        if (!UsesPostgreSql(context) || ids.Length == 0)
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM license_packages WHERE id = ANY({ids}) ORDER BY id FOR UPDATE",
            cancellationToken);
    }

    internal static async Task LockLicenseRequestItemsAsync(
        AppDbContext context,
        IEnumerable<Guid> requestItemIds,
        CancellationToken cancellationToken)
    {
        var ids = requestItemIds.Distinct().OrderBy(x => x).ToArray();
        if (!UsesPostgreSql(context) || ids.Length == 0)
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM license_request_items WHERE id = ANY({ids}) ORDER BY id FOR UPDATE",
            cancellationToken);
    }

    internal static async Task LockLicenseSeatAssignmentsAsync(
        AppDbContext context,
        IEnumerable<Guid> assignmentIds,
        CancellationToken cancellationToken)
    {
        var ids = assignmentIds.Distinct().OrderBy(x => x).ToArray();
        if (!UsesPostgreSql(context) || ids.Length == 0)
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM license_seat_assignments WHERE id = ANY({ids}) ORDER BY id FOR UPDATE",
            cancellationToken);
    }

    internal static async Task WriteAuditAsync(
        AppDbContext context,
        string action,
        string entityName,
        Guid entityId,
        string description,
        Guid? actorUserId,
        string? actorUserName,
        string? actorIpAddress,
        string? actorUserAgent,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                ActorUserId = actorUserId,
                ActorUserName = actorUserName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId.ToString(),
                Description = Truncate(description, AuditDescriptionMaxLength),
                IpAddress = Truncate(actorIpAddress, AuditIpAddressMaxLength),
                UserAgent = Truncate(actorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);
    }

    internal static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
