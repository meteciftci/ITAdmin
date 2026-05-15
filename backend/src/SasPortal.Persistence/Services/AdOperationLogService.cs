using SasPortal.Application.Abstractions.Services;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class AdOperationLogService(AppDbContext context) : IAdOperationLogService
{
    private const int ErrorMessageMaxLength = 2000;
    private const int IpAddressMaxLength = 64;
    private const int UserAgentMaxLength = 1024;

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
