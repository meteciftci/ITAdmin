namespace ITAdmin.Application.Common.Models.LicenseManagement;

public sealed record DirectoryOrganizationalUnitLookupItem(
    string ObjectGuid,
    string DisplayName,
    string? Name,
    string DistinguishedName);

public sealed record DirectoryOrganizationalUnitSearchResult(
    bool IsSuccess,
    string? Message,
    IReadOnlyList<DirectoryOrganizationalUnitLookupItem> Items);
