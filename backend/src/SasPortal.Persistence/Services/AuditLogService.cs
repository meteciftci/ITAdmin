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

        var actions = NormalizeFilterValues(query.Actions, query.Action);
        if (actions.Count > 0)
        {
            logsQuery = logsQuery.Where(x => actions.Contains(x.Action));
        }

        var entityNames = NormalizeFilterValues(query.EntityNames, query.EntityName);
        if (entityNames.Count > 0)
        {
            logsQuery = logsQuery.Where(x => entityNames.Contains(x.EntityName));
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
            logsQuery = ApplySearch(logsQuery, query.Search);
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

    private IQueryable<Domain.Entities.AuditLog> ApplySearch(
        IQueryable<Domain.Entities.AuditLog> logsQuery,
        string search)
    {
        if (context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var pattern = BuildILikeContainsPattern(search);
            return logsQuery.Where(x =>
                EF.Functions.ILike(x.Action, pattern)
                || EF.Functions.ILike(x.EntityName, pattern)
                || (x.EntityId != null && EF.Functions.ILike(x.EntityId, pattern))
                || (x.Description != null && EF.Functions.ILike(x.Description, pattern))
                || (x.ActorUserName != null && EF.Functions.ILike(x.ActorUserName, pattern)));
        }

        var loweredPattern = BuildILikeContainsPattern(search).ToLowerInvariant();
        return logsQuery.Where(x =>
            EF.Functions.Like(x.Action.ToLower(), loweredPattern)
            || EF.Functions.Like(x.EntityName.ToLower(), loweredPattern)
            || (x.EntityId != null && EF.Functions.Like(x.EntityId.ToLower(), loweredPattern))
            || (x.Description != null && EF.Functions.Like(x.Description.ToLower(), loweredPattern))
            || (x.ActorUserName != null && EF.Functions.Like(x.ActorUserName.ToLower(), loweredPattern)));
    }

    private static List<string> NormalizeFilterValues(
        IReadOnlyList<string>? values,
        string? singleValue)
    {
        var normalized = new List<string>();

        if (values is not null)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                normalized.Add(value.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(singleValue))
        {
            normalized.Add(singleValue.Trim());
        }

        return normalized
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
