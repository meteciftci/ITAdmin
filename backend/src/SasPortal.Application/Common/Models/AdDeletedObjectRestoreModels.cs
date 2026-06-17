namespace SasPortal.Application.Common.Models;

public sealed record AdDeletedObjectRestoreRequest(
    Guid ObjectGuid,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent,
    AdDeletedObjectRestoreTargetMode RestoreTargetMode = AdDeletedObjectRestoreTargetMode.OriginalLocation,
    string? TargetPathDistinguishedName = null);

public sealed record AdDeletedObjectRestoreItem(
    string ObjectId,
    AdDeletedObjectType ObjectType,
    string? Name,
    string? SamAccountName,
    string DistinguishedName,
    string? RestoredParent,
    string? RestoredRdn);

public sealed record AdDeletedObjectRestoreResult(
    bool IsSuccess,
    string Message,
    AdDeletedObjectRestoreItem? RestoredObject,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
