namespace SasPortal.Application.Common.Models;

public sealed record MoveAdGroupOuRequest(
    Guid GroupId,
    string TargetOuDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record MoveAdGroupOuResult(
    bool IsSuccess,
    string Message,
    string? GroupId,
    string? DisplayName,
    string? Name,
    string? SamAccountName,
    string? DistinguishedName,
    string? PreviousDistinguishedName,
    string? TargetOuDistinguishedName,
    AdDirectoryFailureKind? FailureKind = null);
