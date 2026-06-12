namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdComputerGroupMembershipItemResponse(
    string Id,
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description,
    bool IsDirect);

public sealed record AdComputerDirectGroupMembershipsResponse(
    string ComputerId,
    string? Name,
    string? SamAccountName,
    string? DnsHostName,
    string? DistinguishedName,
    IReadOnlyList<AdComputerGroupMembershipItemResponse> Groups);

public sealed record AdComputerGroupCandidateItemResponse(
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description);

public sealed record AdComputerGroupCandidateSearchResponse(
    IReadOnlyList<AdComputerGroupCandidateItemResponse> Items);

public sealed record AdComputerGroupMutationRequest(
    string GroupDistinguishedName);

public sealed record AdComputerGroupOperationResponse(
    bool Success,
    string Message,
    string? ComputerId,
    string? ComputerName,
    string? ComputerSamAccountName,
    string? GroupDistinguishedName,
    string? GroupName,
    string? GroupDisplayName,
    string? GroupSamAccountName);
