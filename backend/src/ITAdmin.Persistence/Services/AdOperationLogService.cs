using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class AdOperationLogService(AppDbContext context) : IAdOperationLogService
{
    private const int OperationTypeMaxLength = 64;
    private const int TargetObjectTypeMaxLength = 64;
    private const int TargetDistinguishedNameMaxLength = 1000;
    private const int TargetObjectGuidMaxLength = 64;
    private const int TargetSamAccountNameMaxLength = 100;
    private const int StatusMaxLength = 32;
    private const int ErrorCodeMaxLength = 64;
    private const int ErrorMessageMaxLength = 2000;
    private const int DomainControllerMaxLength = 250;
    private const int ActorUserNameMaxLength = 100;
    private const int IpAddressMaxLength = 64;
    private const int UserAgentMaxLength = 1024;
    private const int CorrelationIdMaxLength = 64;
    private const int MaxPageSize = 100;

    public async Task WriteAsync(AdOperationLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry.OperationType))
        {
            return;
        }

        var log = new AdOperationLog
        {
            OperationType = TruncateRequired(entry.OperationType, OperationTypeMaxLength),
            Status = TruncateRequired(
                string.IsNullOrWhiteSpace(entry.Status) ? "Succeeded" : entry.Status,
                StatusMaxLength),
            TargetObjectType = TruncateNullable(entry.TargetObjectType, TargetObjectTypeMaxLength),
            TargetDistinguishedName = TruncateNullable(
                entry.TargetDistinguishedName,
                TargetDistinguishedNameMaxLength),
            TargetObjectGuid = TruncateNullable(entry.TargetObjectGuid, TargetObjectGuidMaxLength),
            TargetSamAccountName = TruncateNullable(entry.TargetSamAccountName, TargetSamAccountNameMaxLength),
            ErrorCode = TruncateNullable(entry.ErrorCode, ErrorCodeMaxLength),
            ErrorMessage = TruncateNullable(entry.ErrorMessage, ErrorMessageMaxLength),
            DomainController = TruncateNullable(entry.DomainController, DomainControllerMaxLength),
            RequestSummaryJson = NormalizeNullable(entry.RequestSummaryJson),
            BeforeSnapshotJson = NormalizeNullable(entry.BeforeSnapshotJson),
            AfterSnapshotJson = NormalizeNullable(entry.AfterSnapshotJson),
            ActorUserId = entry.ActorUserId,
            ActorUserName = TruncateNullable(entry.ActorUserName, ActorUserNameMaxLength),
            IpAddress = TruncateNullable(entry.IpAddress, IpAddressMaxLength),
            UserAgent = TruncateNullable(entry.UserAgent, UserAgentMaxLength),
            CorrelationId = TruncateNullable(entry.CorrelationId, CorrelationIdMaxLength),
            CreatedAt = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero)
        };

        await context.AdOperationLogs.AddAsync(log, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
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
            logsQuery = ApplyTargetObjectGuidFilter(logsQuery, query.TargetObjectGuid.Trim());
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

    private static IQueryable<AdOperationLog> ApplyTargetObjectGuidFilter(
        IQueryable<AdOperationLog> logsQuery,
        string targetObjectGuid)
    {
        if (!Guid.TryParse(targetObjectGuid, out var parsedGuid))
        {
            return logsQuery.Where(static _ => false);
        }

        var canonical = parsedGuid.ToString("D");
        return logsQuery.Where(x => x.TargetObjectGuid == canonical);
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

    private static string TruncateRequired(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
