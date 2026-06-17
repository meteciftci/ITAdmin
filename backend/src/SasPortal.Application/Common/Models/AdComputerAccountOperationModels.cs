namespace SasPortal.Application.Common.Models;

public sealed record AdComputerAccountOperationRequest(
    Guid ComputerId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AdComputerAccountOperationResult(
    bool IsSuccess,
    string Message,
    AdComputerDetail? Computer = null,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
