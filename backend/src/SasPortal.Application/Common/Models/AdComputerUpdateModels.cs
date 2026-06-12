namespace SasPortal.Application.Common.Models;

public sealed record UpdateAdComputerRequest(
    Guid ComputerId,
    string? Description,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateAdComputerResult(
    bool IsSuccess,
    string Message,
    AdComputerDetail? Computer = null,
    AdDirectoryFailureKind? FailureKind = null);
