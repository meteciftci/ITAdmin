using ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDirectoryOrganizationalUnitLookupReadinessService
{
    Task<DirectoryUserLookupReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}

public interface IDirectoryOrganizationalUnitLookupService
{
    Task<DirectoryOrganizationalUnitSearchResult> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default);
}
