using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdAttributeMappingService
{
    Task<IReadOnlyList<AdAttributeMappingItem>> GetMappingsAsync(CancellationToken cancellationToken = default);

    Task<AdAttributeMappingResult> CreateAsync(
        CreateAdAttributeMappingRequest request,
        CancellationToken cancellationToken = default);

    Task<AdAttributeMappingResult> UpdateAsync(
        UpdateAdAttributeMappingRequest request,
        CancellationToken cancellationToken = default);

    Task<AdAttributeMappingResult> DeleteAsync(
        DeleteAdAttributeMappingRequest request,
        CancellationToken cancellationToken = default);
}
