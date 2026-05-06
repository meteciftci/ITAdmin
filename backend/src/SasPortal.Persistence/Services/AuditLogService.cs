using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class AuditLogService(AppDbContext context) : IAuditLogService
{
    private const int FilterOptionLimit = 100;

    public async Task<PagedResult<AuditLogListItem>> GetAuditLogsAsync(
        AuditLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        var logsQuery = context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            logsQuery = logsQuery.Where(x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var entityName = query.EntityName.Trim();
            logsQuery = logsQuery.Where(x => x.EntityName == entityName);
        }

        if (query.ActorUserId.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.ActorUserId == query.ActorUserId.Value);
        }

        var fromUtc = query.From?.ToUniversalTime();
        var toUtc = query.To?.ToUniversalTime();

        if (fromUtc.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            logsQuery = logsQuery.Where(x =>
                EF.Functions.ILike(x.Action, pattern)
                || EF.Functions.ILike(x.EntityName, pattern)
                || (x.EntityId != null && EF.Functions.ILike(x.EntityId, pattern))
                || (x.Description != null && EF.Functions.ILike(x.Description, pattern))
                || (x.ActorUserName != null && EF.Functions.ILike(x.ActorUserName, pattern)));
        }

        var totalCount = await logsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await logsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogListItem(
                x.Id,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Description,
                x.ActorUserId,
                x.ActorUserName,
                x.IpAddress,
                x.UserAgent,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<AuditLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var logsQuery = context.AuditLogs.AsNoTracking();

        var actions = await logsQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.Action))
            .Select(x => x.Action.Trim())
            .Distinct()
            .OrderBy(x => x)
            .Take(FilterOptionLimit)
            .ToListAsync(cancellationToken);

        var entityNames = await logsQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.EntityName))
            .Select(x => x.EntityName.Trim())
            .Distinct()
            .OrderBy(x => x)
            .Take(FilterOptionLimit)
            .ToListAsync(cancellationToken);

        return new AuditLogFilterOptions(actions, entityNames);
    }

    private static string BuildILikeContainsPattern(string search)
    {
        var trimmed = search.Trim();
        return $"%{trimmed}%";
    }
}
