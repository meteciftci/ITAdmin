namespace SasPortal.Application.Common.Models;

public sealed record DeleteAdGroupRequest(
    Guid GroupId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record DeleteAdGroupResult(
    bool IsSuccess,
    string Message,
    string? DeletedGroupId,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
