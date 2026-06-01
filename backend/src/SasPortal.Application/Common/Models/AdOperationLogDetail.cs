namespace SasPortal.Application.Common.Models;

public sealed record AdOperationLogDetail(
    Guid Id,
    DateTimeOffset CreatedAt,
    string OperationType,
    string Status,
    string? TargetObjectType,
    string? TargetObjectGuid,
    string? TargetDistinguishedName,
    string? TargetSamAccountName,
    string? ErrorCode,
    string? ErrorMessage,
    string? DomainController,
    string? RequestSummaryJson,
    string? BeforeSnapshotJson,
    string? AfterSnapshotJson,
    Guid? ActorUserId,
    string? ActorUserName,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId);
