namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdOrganizationalUnitManageListItemResponse(
    string ObjectGuid,
    string? Name,
    string? Ou,
    string DistinguishedName,
    string? ParentDistinguishedName,
    string CanonicalName,
    int ChildOuCount,
    int UserCount,
    int GroupCount,
    int ComputerCount);

public sealed record AdOrganizationalUnitManageListResponse(
    IReadOnlyList<AdOrganizationalUnitManageListItemResponse> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdOrganizationalUnitChildListItemResponse(
    string ObjectGuid,
    string? Name,
    string? Ou,
    string DistinguishedName,
    string CanonicalName);

public sealed record AdOrganizationalUnitContentSummaryResponse(
    int ChildOuCount,
    int UserCount,
    int GroupCount,
    int ComputerCount);

public sealed record AdOrganizationalUnitDetailResponse(
    string ObjectGuid,
    string? Name,
    string? Ou,
    string? DisplayName,
    string DistinguishedName,
    string? ParentDistinguishedName,
    string CanonicalName,
    AdOrganizationalUnitContentSummaryResponse ContentSummary,
    IReadOnlyList<AdOrganizationalUnitChildListItemResponse> ChildOrganizationalUnits);

public sealed class CreateAdOrganizationalUnitRequest
{
    public string Name { get; init; } = string.Empty;
    public string ParentDistinguishedName { get; init; } = string.Empty;
}

public sealed class RenameAdOrganizationalUnitRequest
{
    public string Name { get; init; } = string.Empty;
}

public sealed class MoveAdOrganizationalUnitRequest
{
    public string TargetParentDistinguishedName { get; init; } = string.Empty;
}

public sealed record CreateAdOrganizationalUnitResponse(
    bool Success,
    string MessageKey,
    AdOrganizationalUnitDetailResponse? OrganizationalUnit,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record RenameAdOrganizationalUnitResponse(
    bool Success,
    string MessageKey,
    AdOrganizationalUnitDetailResponse? OrganizationalUnit,
    string? PreviousDistinguishedName,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record MoveAdOrganizationalUnitResponse(
    bool Success,
    string MessageKey,
    AdOrganizationalUnitDetailResponse? OrganizationalUnit,
    string? PreviousDistinguishedName,
    string? TargetParentDistinguishedName,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record DeleteAdOrganizationalUnitResponse(
    bool Success,
    string MessageKey,
    string? DeletedOrganizationalUnitId,
    IReadOnlyDictionary<string, object>? MessageParams = null);
