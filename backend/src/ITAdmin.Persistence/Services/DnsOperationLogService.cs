using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Persistence.Services;

public sealed class DnsOperationLogService(AppDbContext context) : IDnsOperationLogService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<DnsOperationLogListItemModel>> GetAsync(
        DnsOperationLogQuery query, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Clamp(query.PageNumber, 1, 1_000_000);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var source = context.DnsOperationLogs.AsNoTracking().AsQueryable();

        if (query.ServerId is { } serverId)
            source = source.Where(x => x.DnsServerId == serverId);
        if (Normalize(query.OperationType, 64) is { } operationType)
            source = source.Where(x => x.OperationType == operationType);
        if (Normalize(query.Status, 32) is { } status)
            source = source.Where(x => x.Status == status);
        if (Normalize(query.TargetSearch, 512) is { } targetSearch)
            source = ApplyTargetSearch(source, targetSearch);
        if (Normalize(query.ActorUserName, 100) is { } actor)
            source = ApplyActorSearch(source, actor);
        if (query.DateFrom is { } dateFrom)
            source = source.Where(x => x.CreatedAt >= dateFrom.ToUniversalTime());
        if (query.DateTo is { } dateTo)
            source = source.Where(x => x.CreatedAt <= dateTo.ToUniversalTime());

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new DnsOperationLogListItemModel(
                x.Id, x.CreatedAt, x.DnsServerId, x.ServerDisplayName,
                x.OperationType, x.Status, x.ZoneName, x.RecordName, x.RecordType,
                x.ActorUserName, x.ErrorCode, x.ErrorMessage,
                x.RequestSummaryJson != null && x.RequestSummaryJson != string.Empty,
                x.BeforeSnapshotJson != null && x.BeforeSnapshotJson != string.Empty,
                x.AfterSnapshotJson != null && x.AfterSnapshotJson != string.Empty))
            .ToListAsync(cancellationToken);
        return new(items, pageNumber, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public Task<DnsOperationLogDetailModel?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        context.DnsOperationLogs.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new DnsOperationLogDetailModel(
                x.Id, x.CreatedAt, x.DnsServerId, x.ServerDisplayName,
                x.OperationType, x.Status, x.ZoneName, x.RecordName, x.RecordType,
                x.RequestSummaryJson, x.BeforeSnapshotJson, x.AfterSnapshotJson,
                x.ErrorCode, x.ErrorMessage, x.ActorUserId, x.ActorUserName,
                x.IpAddress, x.UserAgent, x.CorrelationId))
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<DnsOperationLog> ApplyTargetSearch(
        IQueryable<DnsOperationLog> source, string search)
    {
        if (IsNpgsql())
        {
            var pattern = $"%{search}%";
            return source.Where(x =>
                (x.ServerDisplayName != null && EF.Functions.ILike(x.ServerDisplayName, pattern))
                || (x.ZoneName != null && EF.Functions.ILike(x.ZoneName, pattern))
                || (x.RecordName != null && EF.Functions.ILike(x.RecordName, pattern))
                || (x.RecordType != null && EF.Functions.ILike(x.RecordType, pattern)));
        }
        var normalized = search.ToLowerInvariant();
        return source.Where(x =>
            (x.ServerDisplayName != null && x.ServerDisplayName.ToLower().Contains(normalized))
            || (x.ZoneName != null && x.ZoneName.ToLower().Contains(normalized))
            || (x.RecordName != null && x.RecordName.ToLower().Contains(normalized))
            || (x.RecordType != null && x.RecordType.ToLower().Contains(normalized)));
    }

    private IQueryable<DnsOperationLog> ApplyActorSearch(
        IQueryable<DnsOperationLog> source, string actor)
    {
        if (IsNpgsql())
        {
            var pattern = $"%{actor}%";
            return source.Where(x => x.ActorUserName != null
                && EF.Functions.ILike(x.ActorUserName, pattern));
        }
        var normalized = actor.ToLowerInvariant();
        return source.Where(x => x.ActorUserName != null
            && x.ActorUserName.ToLower().Contains(normalized));
    }

    private bool IsNpgsql() =>
        context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
}
