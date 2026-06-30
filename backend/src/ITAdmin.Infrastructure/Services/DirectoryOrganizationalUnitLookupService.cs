using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Infrastructure.Services;

public sealed class DirectoryOrganizationalUnitLookupService(
    IAdUserDirectoryService directoryService) : IDirectoryOrganizationalUnitLookupService
{
    public async Task<DirectoryOrganizationalUnitSearchResult> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (!AdLdapAttributeCatalog.IsSearchTermValid(search))
        {
            return new DirectoryOrganizationalUnitSearchResult(true, null, []);
        }

        var result = await directoryService.SearchOrganizationalUnitsAsync(
            new AdOrganizationalUnitSearchQuery(search, 50),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return new DirectoryOrganizationalUnitSearchResult(
                false,
                "AD OU arama şu anda kullanılamıyor.",
                []);
        }

        var items = result.Page.Items
            .Select(MapItem)
            .Where(item => item is not null)
            .Cast<DirectoryOrganizationalUnitLookupItem>()
            .ToList();

        return new DirectoryOrganizationalUnitSearchResult(true, null, items);
    }

    private static DirectoryOrganizationalUnitLookupItem? MapItem(AdOrganizationalUnitListItem item)
    {
        if (string.IsNullOrWhiteSpace(item.DistinguishedName))
        {
            return null;
        }

        var displayName = item.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = item.Name?.Trim() ?? item.Ou?.Trim() ?? item.Label.Trim();
        }

        var objectGuid = item.ObjectGuid?.Trim();
        if (string.IsNullOrWhiteSpace(objectGuid))
        {
            objectGuid = item.DistinguishedName;
        }

        return new DirectoryOrganizationalUnitLookupItem(
            objectGuid,
            displayName,
            item.Name,
            item.DistinguishedName);
    }
}
