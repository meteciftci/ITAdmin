namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdComputerListItemResponse(
    string Id,
    string Name,
    string? SamAccountName,
    string? DnsHostName,
    string? OperatingSystem,
    string DistinguishedName,
    bool IsEnabled,
    DateTimeOffset? WhenChanged);

public sealed record AdComputerListResponse(
    IReadOnlyList<AdComputerListItemResponse> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdComputerOperatingSystemOptionsResponse(
    IReadOnlyList<string> Items);

public sealed record AdComputerMemberOfItemResponse(
    string DistinguishedName,
    string? Name,
    string? SamAccountName);

public sealed record AdComputerDetailResponse(
    string Id,
    string Name,
    string? Cn,
    string? SamAccountName,
    string? DnsHostName,
    string DistinguishedName,
    string? ParentOuDistinguishedName,
    string? Description,
    string? OperatingSystem,
    string? OperatingSystemVersion,
    string? OperatingSystemServicePack,
    string? ManagedByDistinguishedName,
    string? ManagedByDisplayName,
    DateTimeOffset? LastLogonAt,
    DateTimeOffset? WhenCreated,
    DateTimeOffset? WhenChanged,
    int? UserAccountControl,
    bool IsEnabled,
    int? PrimaryGroupId,
    int MemberOfCount,
    IReadOnlyList<AdComputerMemberOfItemResponse> MemberOf,
    bool MemberOfTruncated);
