namespace SasPortal.Application.Common.Models;

public sealed record CreateAdAttributeMappingRequest(
    string LogicalField,
    string DisplayName,
    string AttributeName,
    bool IsEnabled,
    bool IsEditable,
    bool IsSensitive,
    string ValidationType,
    string MaskingStrategy,
    int SortOrder,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
