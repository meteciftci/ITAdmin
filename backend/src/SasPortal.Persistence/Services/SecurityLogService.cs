using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class SecurityLogService(AppDbContext context) : ISecurityLogService
{
    private const int FilterOptionLimit = 100;

    public async Task<PagedResult<SecurityLogListItem>> GetSecurityLogsAsync(
        SecurityLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        var logsQuery = context.SecurityLogs.AsNoTracking().AsQueryable();

        var eventTypes = NormalizeFilterValues(query.EventTypes);
        if (eventTypes.Count > 0)
        {
            logsQuery = logsQuery.Where(x => eventTypes.Contains(x.EventType));
        }

        var severities = NormalizeFilterValues(query.Severities);
        if (severities.Count > 0)
        {
            logsQuery = logsQuery.Where(x => severities.Contains(x.Severity));
        }

        if (query.UserId.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.UserId == query.UserId.Value);
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
            .Select(x => new SecurityLogListItem(
                x.Id,
                x.EventType,
                x.Severity,
                x.UserId,
                x.UserName,
                x.IpAddress,
                x.UserAgent,
                x.Description,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<SecurityLogListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<SecurityLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var logsQuery = context.SecurityLogs.AsNoTracking();

        var eventTypes = await logsQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.EventType))
            .Select(x => x.EventType.Trim())
            .Distinct()
            .OrderBy(x => x)
            .Take(FilterOptionLimit)
            .ToListAsync(cancellationToken);

        var severities = await logsQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.Severity))
            .Select(x => x.Severity.Trim())
            .Distinct()
            .OrderBy(x => x)
            .Take(FilterOptionLimit)
            .ToListAsync(cancellationToken);

        return new SecurityLogFilterOptions(eventTypes, severities);
    }

    private static string BuildILikeContainsPattern(string search)
    {
        var trimmed = search.Trim();
        return $"%{trimmed}%";
    }

    private IQueryable<Domain.Entities.SecurityLog> ApplySearch(
        IQueryable<Domain.Entities.SecurityLog> logsQuery,
        string search)
    {
        if (context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var pattern = BuildILikeContainsPattern(search);
            return logsQuery.Where(x =>
                EF.Functions.ILike(x.EventType, pattern)
                || EF.Functions.ILike(x.Severity, pattern)
                || (x.UserName != null && EF.Functions.ILike(x.UserName, pattern))
                || (x.IpAddress != null && EF.Functions.ILike(x.IpAddress, pattern))
                || (x.Description != null && EF.Functions.ILike(x.Description, pattern)));
        }

        var loweredPattern = BuildILikeContainsPattern(search).ToLowerInvariant();
        return logsQuery.Where(x =>
            EF.Functions.Like(x.EventType.ToLower(), loweredPattern)
            || EF.Functions.Like(x.Severity.ToLower(), loweredPattern)
            || (x.UserName != null && EF.Functions.Like(x.UserName.ToLower(), loweredPattern))
            || (x.IpAddress != null && EF.Functions.Like(x.IpAddress.ToLower(), loweredPattern))
            || (x.Description != null && EF.Functions.Like(x.Description.ToLower(), loweredPattern)));
    }

    private static List<string> NormalizeFilterValues(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
