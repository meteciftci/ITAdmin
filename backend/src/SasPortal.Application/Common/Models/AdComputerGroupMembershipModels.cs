namespace SasPortal.Application.Common.Models;

public sealed record AdComputerGroupMembershipRequest(
    Guid ComputerId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AdComputerGroupSearchRequest(
    Guid ComputerId,
    string? Query);

public sealed record AddAdComputerToGroupRequest(
    Guid ComputerId,
    string GroupDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record RemoveAdComputerFromGroupRequest(
    Guid ComputerId,
    string GroupDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AdComputerGroupMembershipItem(
    string Id,
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description,
    bool IsDirect);

public sealed record AdComputerGroupCandidateItem(
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description);

public sealed record AdComputerGroupMembershipResult(
    bool IsSuccess,
    string Message,
    string? ComputerId,
    string? Name,
    string? SamAccountName,
    string? DnsHostName,
    string? DistinguishedName,
    IReadOnlyList<AdComputerGroupMembershipItem>? Groups,
    AdDirectoryFailureKind? FailureKind = null);

public sealed record AdComputerGroupSearchResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<AdComputerGroupCandidateItem>? Items,
    AdDirectoryFailureKind? FailureKind = null);

public sealed record AdComputerGroupOperationResult(
    bool IsSuccess,
    string Message,
    string? ComputerId,
    string? ComputerName,
    string? ComputerSamAccountName,
    string? GroupDistinguishedName,
    string? GroupName,
    string? GroupDisplayName,
    string? GroupSamAccountName,
    AdDirectoryFailureKind? FailureKind = null);
