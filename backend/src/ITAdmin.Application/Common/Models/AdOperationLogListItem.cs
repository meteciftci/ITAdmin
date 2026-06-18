namespace ITAdmin.Application.Common.Models;

public sealed record AdOperationLogListItem(
    Guid Id,
    DateTimeOffset CreatedAt,
    string OperationType,
    string Status,
    string? TargetObjectType,
    string? TargetObjectGuid,
    string? TargetDistinguishedName,
    string? TargetSamAccountName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? IpAddress,
    string? DomainController,
    string? ErrorMessage,
    bool HasError,
    bool HasBeforeSnapshot,
    bool HasAfterSnapshot,
    bool HasRequestSummary);
