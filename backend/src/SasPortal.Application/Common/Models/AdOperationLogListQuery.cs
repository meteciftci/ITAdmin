namespace SasPortal.Application.Common.Models;

public sealed record AdOperationLogListQuery(
    string? OperationType,
    string? Status,
    string? TargetObjectType,
    string? TargetSamAccountName,
    string? TargetObjectGuid,
    string? ActorUserName,
    string? DomainController,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int PageNumber,
    int PageSize);
