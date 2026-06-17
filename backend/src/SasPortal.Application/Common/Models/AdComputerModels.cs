namespace SasPortal.Application.Common.Models;

public sealed record AdComputerListQuery(
    string? Search,
    AdUserStatusFilter Status,
    string? OperatingSystem,
    int PageNumber,
    int PageSize);

public sealed record AdComputerOperatingSystemOptionsPage(
    IReadOnlyList<string> Items);

public sealed record AdComputerOperatingSystemOptionsResult(
    bool IsSuccess,
    string Message,
    AdComputerOperatingSystemOptionsPage? Page,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdComputerListItem(
    string Id,
    string Name,
    string? SamAccountName,
    string? DnsHostName,
    string? OperatingSystem,
    string DistinguishedName,
    bool IsEnabled,
    DateTimeOffset? WhenChanged);

public sealed record AdComputerSearchPage(
    IReadOnlyList<AdComputerListItem> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdComputerMemberOfItem(
    string DistinguishedName,
    string? Name,
    string? SamAccountName);

public sealed record AdComputerDetail(
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
    IReadOnlyList<AdComputerMemberOfItem> MemberOf,
    bool MemberOfTruncated);

public sealed record AdComputerDirectoryListResult(
    bool IsSuccess,
    string Message,
    AdComputerSearchPage? Page,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdComputerDirectoryDetailResult(
    bool IsSuccess,
    string Message,
    AdComputerDetail? Computer,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
