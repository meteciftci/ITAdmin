using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface ILicenseManagementOverviewService
{
    Task<LicenseManagementOverviewSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public interface ILicenseManagementSettingsService
{
    Task<LicenseManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<UpdateLicenseManagementSettingsResult> UpdateSettingsAsync(
        UpdateLicenseManagementSettingsRequest request,
        CancellationToken cancellationToken = default);
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

public interface ILicenseProductCategoryService
{
    Task<PagedResult<LicenseProductCategoryListItem>> GetListAsync(
        LicenseProductCategoryListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LicenseProductCategoryListItem>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);

    Task<LicenseProductCategoryDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LicenseProductCategoryOperationResult> CreateAsync(
        CreateLicenseProductCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseProductCategoryOperationResult> UpdateAsync(
        UpdateLicenseProductCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseProductCategoryOperationResult> UpdateStatusAsync(
        UpdateLicenseProductCategoryStatusRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILicensePurchaseService
{
    Task<PagedResult<LicensePurchaseListItem>> GetListAsync(
        LicensePurchaseListQuery query,
        CancellationToken cancellationToken = default);

    Task<LicensePurchaseDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LicensePurchaseOperationResult> CreateAsync(
        CreateLicensePurchaseRequest request,
        CancellationToken cancellationToken = default);

    Task<LicensePurchaseOperationResult> UpdateAsync(
        UpdateLicensePurchaseRequest request,
        CancellationToken cancellationToken = default);

    Task<LicensePurchaseOperationResult> UpdateStatusAsync(
        UpdateLicensePurchaseStatusRequest request,
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

public interface ILicenseRequestService
{
    Task<PagedResult<LicenseRequestListItem>> GetListAsync(
        LicenseRequestListQuery query,
        CancellationToken cancellationToken = default);

    Task<LicenseRequestDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LicenseRequestOperationResult> CreateAsync(
        CreateLicenseRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseRequestOperationResult> UpdateAsync(
        UpdateLicenseRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseRequestOperationResult> UpdateStatusAsync(
        UpdateLicenseRequestStatusRequest request,
        CancellationToken cancellationToken = default);
}
