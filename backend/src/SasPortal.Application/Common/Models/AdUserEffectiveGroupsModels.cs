namespace SasPortal.Application.Common.Models;

public sealed record AdUserEffectiveGroupsRequest(
    Guid UserId,
    int? MaxDepth);

public sealed record AdUserEffectiveGroupsResult(
    bool IsSuccess,
    string Message,
    string? UserId,
    string? DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DistinguishedName,
    IReadOnlyList<AdEffectiveGroupSummaryItem>? DirectGroups,
    IReadOnlyList<AdEffectiveGroupNestedItem>? EffectiveGroups,
    int MaxDepth,
    bool Truncated,
    string? TruncatedReason,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdEffectiveGroupSummaryItem(
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description);

public sealed record AdEffectiveGroupNestedItem(
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description,
    int Depth,
    bool IsDirect,
    IReadOnlyList<AdMembershipPathNode> Path);

public sealed record AdMembershipPathNode(
    string Type,
    string Name,
    string? DisplayName,
    string? SamAccountName,
    string DistinguishedName);

public static class AdMembershipPathNodeTypes
{
    public const string User = "User";
    public const string Group = "Group";
}

public static class AdEffectiveGroupTruncatedReasons
{
    public const string ResultLimitExceeded = "ResultLimitExceeded";
}
