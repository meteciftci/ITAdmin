namespace ITAdmin.Application.Common.Models;

public sealed record AdComputerAccountOperationRequest(
    Guid ComputerId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AdComputerAccountOperationResult(
    bool IsSuccess,
    string MessageKey,
    AdComputerDetail? Computer = null,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
