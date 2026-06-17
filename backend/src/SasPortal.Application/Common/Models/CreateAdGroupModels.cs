namespace SasPortal.Application.Common.Models;

public sealed record CreateAdGroupRequest(
    string DisplayName,
    string Name,
    string SamAccountName,
    string? Description,
    string GroupScope,
    string TargetOuDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record CreateAdGroupResult(
    bool IsSuccess,
    string Message,
    AdGroupDetail? Group,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
