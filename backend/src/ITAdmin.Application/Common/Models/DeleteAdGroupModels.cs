namespace ITAdmin.Application.Common.Models;

public sealed record DeleteAdGroupRequest(
    Guid GroupId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record DeleteAdGroupResult(
    bool IsSuccess,
    string MessageKey,
    string? DeletedGroupId,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
