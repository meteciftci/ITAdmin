namespace SasPortal.Application.Common.Models;

public sealed record MoveAdUserOuRequest(
    Guid UserId,
    string TargetOuDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record MoveAdUserOuResult(
    bool IsSuccess,
    string Message,
    string? UserId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DistinguishedName,
    string? PreviousDistinguishedName,
    string? TargetOuDistinguishedName,
    AdDirectoryFailureKind? FailureKind = null);
