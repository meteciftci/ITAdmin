namespace SasPortal.Application.Common.Models;

public sealed record DeleteAdComputerRequest(
    Guid ComputerId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record DeleteAdComputerResult(
    bool IsSuccess,
    string MessageKey,
    string? DeletedComputerId,
    string? DeletedComputerName,
    string? DeletedDistinguishedName,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
