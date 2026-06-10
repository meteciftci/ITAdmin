using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdComputerDirectoryService
{
    Task<AdComputerDirectoryListResult> SearchComputersAsync(
        AdComputerListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdComputerDirectoryDetailResult> GetComputerByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<AdOrganizationalUnitSearchResult> SearchComputerOrganizationalUnitsAsync(
        AdOrganizationalUnitSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<AdComputerOperatingSystemOptionsResult> GetComputerOperatingSystemsAsync(
        CancellationToken cancellationToken = default);
}
