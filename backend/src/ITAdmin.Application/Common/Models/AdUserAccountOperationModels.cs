namespace ITAdmin.Application.Common.Models;

public sealed record AdUserAccountOperationRequest(
    Guid UserId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AdUserAccountOperationResult(
    bool IsSuccess,
    string MessageKey,
    string? UserId = null,
    string? SamAccountName = null,
    string? UserPrincipalName = null,
    string? DistinguishedName = null,
    bool? IsEnabled = null,
    bool? IsLockedOut = null,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
