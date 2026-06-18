namespace ITAdmin.Application.Common.Models;

public sealed record UpdateAdAttributeMappingRequest(
    Guid Id,
    string DisplayName,
    string AttributeName,
    bool IsEnabled,
    bool IsEditable,
    bool IsSensitive,
    bool IsSearchable,
    string ValidationType,
    string MaskingStrategy,
    int SortOrder,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
