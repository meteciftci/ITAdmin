namespace SasPortal.Application.Common.Models;

public sealed record AdManagementValidationRequest(
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
