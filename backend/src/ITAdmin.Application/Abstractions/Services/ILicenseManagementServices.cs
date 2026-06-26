using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface ILicenseManagementOverviewService
{
    Task<LicenseManagementOverviewSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public interface ILicenseCompanyService
{
    Task<PagedResult<LicenseCompanyListItem>> GetListAsync(
        LicenseCompanyListQuery query,
        CancellationToken cancellationToken = default);

    Task<LicenseCompanyDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LicenseCompanyOperationResult> CreateAsync(
        CreateLicenseCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseCompanyOperationResult> UpdateAsync(
        UpdateLicenseCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseCompanyOperationResult> UpdateStatusAsync(
        UpdateLicenseCompanyStatusRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILicensedProductService
{
    Task<PagedResult<LicensedProductListItem>> GetListAsync(
        LicensedProductListQuery query,
        CancellationToken cancellationToken = default);

    Task<LicensedProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LicensedProductOperationResult> CreateAsync(
        CreateLicensedProductRequest request,
        CancellationToken cancellationToken = default);

    Task<LicensedProductOperationResult> UpdateAsync(
        UpdateLicensedProductRequest request,
        CancellationToken cancellationToken = default);

    Task<LicensedProductOperationResult> UpdateStatusAsync(
        UpdateLicensedProductStatusRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILicenseAcquisitionService
{
    Task<PagedResult<LicenseAcquisitionListItem>> GetListAsync(
        LicenseAcquisitionListQuery query,
        CancellationToken cancellationToken = default);

    Task<LicenseAcquisitionDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LicenseAcquisitionOperationResult> CreateAsync(
        CreateLicenseAcquisitionRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseAcquisitionOperationResult> UpdateAsync(
        UpdateLicenseAcquisitionRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseAcquisitionOperationResult> UpdateStatusAsync(
        UpdateLicenseAcquisitionStatusRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILicensePackageService
{
    Task<PagedResult<LicensePackageListItem>> GetListAsync(
        LicensePackageListQuery query,
        CancellationToken cancellationToken = default);

    Task<LicensePackageDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LicensePackageOperationResult> CreateAsync(
        CreateLicensePackageRequest request,
        CancellationToken cancellationToken = default);

    Task<LicensePackageOperationResult> UpdateAsync(
        UpdateLicensePackageRequest request,
        CancellationToken cancellationToken = default);

    Task<LicensePackageOperationResult> UpdateStatusAsync(
        UpdateLicensePackageStatusRequest request,
        CancellationToken cancellationToken = default);
}
