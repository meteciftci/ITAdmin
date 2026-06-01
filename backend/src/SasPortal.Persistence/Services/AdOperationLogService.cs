using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class AdOperationLogService(AppDbContext context) : IAdOperationLogService
{
    private const int ErrorMessageMaxLength = 2000;
    private const int IpAddressMaxLength = 64;
    private const int UserAgentMaxLength = 1024;
    private const int MaxPageSize = 100;

    public async Task WriteAsync(AdOperationLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry.OperationType))
        {
            return;
        }

        var log = new AdOperationLog
        {
            OperationType = entry.OperationType.Trim(),
            Status = string.IsNullOrWhiteSpace(entry.Status) ? "Succeeded" : entry.Status.Trim(),
            TargetObjectType = NormalizeNullable(entry.TargetObjectType),
            TargetDistinguishedName = NormalizeNullable(entry.TargetDistinguishedName),
            TargetObjectGuid = NormalizeNullable(entry.TargetObjectGuid),
            TargetSamAccountName = NormalizeNullable(entry.TargetSamAccountName),
            ErrorCode = NormalizeNullable(entry.ErrorCode),
            ErrorMessage = Truncate(NormalizeNullable(entry.ErrorMessage), ErrorMessageMaxLength),
            DomainController = NormalizeNullable(entry.DomainController),
            RequestSummaryJson = NormalizeNullable(entry.RequestSummaryJson),
            BeforeSnapshotJson = NormalizeNullable(entry.BeforeSnapshotJson),
            AfterSnapshotJson = NormalizeNullable(entry.AfterSnapshotJson),
            ActorUserId = entry.ActorUserId,
            ActorUserName = NormalizeNullable(entry.ActorUserName),
            IpAddress = Truncate(NormalizeNullable(entry.IpAddress), IpAddressMaxLength),
            UserAgent = Truncate(NormalizeNullable(entry.UserAgent), UserAgentMaxLength),
            CorrelationId = NormalizeNullable(entry.CorrelationId),
            CreatedAt = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero)
        };

        await context.AdOperationLogs.AddAsync(log, cancellationToken);
    }

    public async Task<PagedResult<AdOperationLogListItem>> GetLogsAsync(
        AdOperationLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => query.PageSize
        };

        var logsQuery = context.AdOperationLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.OperationType))
        {
            var operationType = query.OperationType.Trim();
            logsQuery = logsQuery.Where(x => x.OperationType == operationType);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            logsQuery = logsQuery.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetObjectType))
        {
            var targetObjectType = query.TargetObjectType.Trim();
            logsQuery = logsQuery.Where(x => x.TargetObjectType == targetObjectType);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetSamAccountName))
        {
            logsQuery = ApplyTargetSearch(logsQuery, query.TargetSamAccountName.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(query.TargetObjectGuid))
        {
            logsQuery = ApplyStringContainsFilter(logsQuery, query.TargetObjectGuid.Trim(), StringFilterField.TargetObjectGuid);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorUserName))
        {
            logsQuery = ApplyStringContainsFilter(logsQuery, query.ActorUserName.Trim(), StringFilterField.ActorUserName);
        }

        if (!string.IsNullOrWhiteSpace(query.DomainController))
        {
            logsQuery = ApplyStringContainsFilter(logsQuery, query.DomainController.Trim(), StringFilterField.DomainController);
        }

        var dateFromUtc = query.DateFrom?.ToUniversalTime();
        var dateToUtc = query.DateTo?.ToUniversalTime();

        if (dateFromUtc.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt >= dateFromUtc.Value);
        }

        if (dateToUtc.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt <= dateToUtc.Value);
        }

        var totalCount = await logsQuery.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await logsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdOperationLogListItem(
                x.Id,
                x.CreatedAt,
                x.OperationType,
                x.Status,
                x.TargetObjectType,
                x.TargetObjectGuid,
                x.TargetDistinguishedName,
                x.TargetSamAccountName,
                x.ActorUserId,
                x.ActorUserName,
                x.IpAddress,
                x.DomainController,
                x.ErrorMessage,
                x.Status == AdManagementOperationStatuses.Failed
                    || (x.ErrorMessage != null && x.ErrorMessage != string.Empty),
                x.BeforeSnapshotJson != null && x.BeforeSnapshotJson != string.Empty,
                x.AfterSnapshotJson != null && x.AfterSnapshotJson != string.Empty,
                x.RequestSummaryJson != null && x.RequestSummaryJson != string.Empty))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdOperationLogListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<AdOperationLogDetail?> GetLogByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.AdOperationLogs
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AdOperationLogDetail(
                x.Id,
                x.CreatedAt,
                x.OperationType,
                x.Status,
                x.TargetObjectType,
                x.TargetObjectGuid,
                x.TargetDistinguishedName,
                x.TargetSamAccountName,
                x.ErrorCode,
                x.ErrorMessage,
                x.DomainController,
                x.RequestSummaryJson,
                x.BeforeSnapshotJson,
                x.AfterSnapshotJson,
                x.ActorUserId,
                x.ActorUserName,
                x.IpAddress,
                x.UserAgent,
                x.CorrelationId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<AdOperationLog> ApplyTargetSearch(IQueryable<AdOperationLog> logsQuery, string search)
    {
        if (IsNpgsqlProvider())
        {
            var pattern = BuildContainsPattern(search);
            return logsQuery.Where(x =>
                (x.TargetSamAccountName != null && EF.Functions.ILike(x.TargetSamAccountName, pattern))
                || (x.TargetObjectGuid != null && EF.Functions.ILike(x.TargetObjectGuid, pattern)));
        }

        return logsQuery.Where(x =>
            (x.TargetSamAccountName != null && x.TargetSamAccountName.Contains(search))
            || (x.TargetObjectGuid != null && x.TargetObjectGuid.Contains(search)));
    }

    private enum StringFilterField
    {
        TargetObjectGuid,
        ActorUserName,
        DomainController,
    }

    private IQueryable<AdOperationLog> ApplyStringContainsFilter(
        IQueryable<AdOperationLog> logsQuery,
        string search,
        StringFilterField field)
    {
        if (IsNpgsqlProvider())
        {
            var pattern = BuildContainsPattern(search);
            return field switch
            {
                StringFilterField.TargetObjectGuid => logsQuery.Where(x =>
                    x.TargetObjectGuid != null && EF.Functions.ILike(x.TargetObjectGuid, pattern)),
                StringFilterField.ActorUserName => logsQuery.Where(x =>
                    x.ActorUserName != null && EF.Functions.ILike(x.ActorUserName, pattern)),
                _ => logsQuery.Where(x =>
                    x.DomainController != null && EF.Functions.ILike(x.DomainController, pattern)),
            };
        }

        return field switch
        {
            StringFilterField.TargetObjectGuid => logsQuery.Where(x =>
                x.TargetObjectGuid != null && x.TargetObjectGuid.Contains(search)),
            StringFilterField.ActorUserName => logsQuery.Where(x =>
                x.ActorUserName != null && x.ActorUserName.Contains(search)),
            _ => logsQuery.Where(x =>
                x.DomainController != null && x.DomainController.Contains(search)),
        };
    }

    private bool IsNpgsqlProvider() =>
        context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private static string BuildContainsPattern(string search) => $"%{search.Trim()}%";

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
