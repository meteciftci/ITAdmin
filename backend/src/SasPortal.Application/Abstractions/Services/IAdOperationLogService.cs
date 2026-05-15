namespace SasPortal.Application.Abstractions.Services;

public sealed class AdOperationLogEntry
{
    public string OperationType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? TargetObjectType { get; init; }
    public string? TargetDistinguishedName { get; init; }
    public string? TargetObjectGuid { get; init; }
    public string? TargetSamAccountName { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? DomainController { get; init; }
    public string? RequestSummaryJson { get; init; }
    public string? BeforeSnapshotJson { get; init; }
    public string? AfterSnapshotJson { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorUserName { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? CorrelationId { get; init; }
}

public interface IAdOperationLogService
{
    Task WriteAsync(AdOperationLogEntry entry, CancellationToken cancellationToken = default);
}
