namespace ITAdmin.Application.Common.Models;

public sealed record MoveAdComputerOuRequest(
    Guid ComputerId,
    string TargetOuDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record MoveAdComputerOuResult(
    bool IsSuccess,
    string MessageKey,
    AdComputerDetail? Computer = null,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
