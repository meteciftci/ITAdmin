import { apiClient } from "@/lib/api-client";

import type {
  DirectoryUserLookupReadiness,
  LicenseCompanyDetail,
  LicenseCompanyFormRequest,
  LicenseCompanyListItem,
  LicensedProductDetail,
  LicensedProductFormRequest,
  LicensedProductListItem,
  LicenseManagementOverview,
  LicenseManagementSettings,
  LicensePackageDetail,
  LicensePackageFormRequest,
  LicensePackageListItem,
  LicensePackageStatus,
  LicenseProductCategoryDetail,
  LicenseProductCategoryFormRequest,
  LicenseProductCategoryListItem,
  LicensePurchaseDetail,
  LicensePurchaseFormRequest,
  LicensePurchaseListItem,
  LicensePurchaseStatus,
  LicensePurchaseType,
  LicenseRequestDetail,
  LicenseRequestFormRequest,
  LicenseRequestListItem,
  LicenseRequestSource,
  LicenseRequestStatus,
  PagedResponse,
  UpdateLicenseManagementSettingsRequest,
} from "@/features/license-management/types";

const basePath = "/license-management";

export const LICENSE_MANAGEMENT_SETTINGS_QUERY_KEY = ["license-management", "settings"] as const;

export const getLicenseManagementOverview = async (): Promise<LicenseManagementOverview> => {
  const { data } = await apiClient.get<LicenseManagementOverview>(`${basePath}/overview`);
  return data;
};

export const getLicenseManagementSettings = async (): Promise<LicenseManagementSettings> => {
  const { data } = await apiClient.get<LicenseManagementSettings>(`${basePath}/settings`);
  return data;
};

export const updateLicenseManagementSettings = async (
  request: UpdateLicenseManagementSettingsRequest,
): Promise<LicenseManagementSettings> => {
  const { data } = await apiClient.put<LicenseManagementSettings>(`${basePath}/settings`, request);
  return data;
};

type CompanyListParams = {
  search?: string;
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

export const getLicenseCompanies = async (
  params: CompanyListParams,
): Promise<PagedResponse<LicenseCompanyListItem>> => {
  const { data } = await apiClient.get<PagedResponse<LicenseCompanyListItem>>(
    `${basePath}/companies`,
    { params },
  );
  return data;
};

export const getLicenseCompanyById = async (id: string): Promise<LicenseCompanyDetail> => {
  const { data } = await apiClient.get<LicenseCompanyDetail>(`${basePath}/companies/${id}`);
  return data;
};

export const createLicenseCompany = async (request: LicenseCompanyFormRequest): Promise<void> => {
  await apiClient.post(`${basePath}/companies`, request);
};

export const updateLicenseCompany = async (
  id: string,
  request: LicenseCompanyFormRequest,
): Promise<void> => {
  await apiClient.put(`${basePath}/companies/${id}`, request);
};

export const updateLicenseCompanyStatus = async (
  id: string,
  isActive: boolean,
): Promise<void> => {
  await apiClient.patch(`${basePath}/companies/${id}/status`, { isActive });
};

type CategoryListParams = {
  search?: string;
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

export const getLicenseProductCategories = async (
  params: CategoryListParams,
): Promise<PagedResponse<LicenseProductCategoryListItem>> => {
  const { data } = await apiClient.get<PagedResponse<LicenseProductCategoryListItem>>(
    `${basePath}/product-categories`,
    { params },
  );
  return data;
};

export const getLicenseProductCategoryById = async (
  id: string,
): Promise<LicenseProductCategoryDetail> => {
  const { data } = await apiClient.get<LicenseProductCategoryDetail>(
    `${basePath}/product-categories/${id}`,
  );
  return data;
};

export const createLicenseProductCategory = async (
  request: LicenseProductCategoryFormRequest,
): Promise<void> => {
  await apiClient.post(`${basePath}/product-categories`, request);
};

export const updateLicenseProductCategory = async (
  id: string,
  request: LicenseProductCategoryFormRequest,
): Promise<void> => {
  await apiClient.put(`${basePath}/product-categories/${id}`, request);
};

export const updateLicenseProductCategoryStatus = async (
  id: string,
  isActive: boolean,
): Promise<void> => {
  await apiClient.patch(`${basePath}/product-categories/${id}/status`, { isActive });
};

export const getAllLicenseProductCategories = async (): Promise<LicenseProductCategoryListItem[]> => {
  const { data } = await apiClient.get<LicenseProductCategoryListItem[]>(
    `${basePath}/product-categories/all`,
  );
  return data;
};

type ProductListParams = {
  search?: string;
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

export const getLicensedProducts = async (
  params: ProductListParams,
): Promise<PagedResponse<LicensedProductListItem>> => {
  const { data } = await apiClient.get<PagedResponse<LicensedProductListItem>>(
    `${basePath}/products`,
    { params },
  );
  return data;
};

export const getLicensedProductById = async (id: string): Promise<LicensedProductDetail> => {
  const { data } = await apiClient.get<LicensedProductDetail>(`${basePath}/products/${id}`);
  return data;
};

export const createLicensedProduct = async (request: LicensedProductFormRequest): Promise<void> => {
  await apiClient.post(`${basePath}/products`, request);
};

export const updateLicensedProduct = async (
  id: string,
  request: LicensedProductFormRequest,
): Promise<void> => {
  await apiClient.put(`${basePath}/products/${id}`, request);
};

export const updateLicensedProductStatus = async (
  id: string,
  isActive: boolean,
): Promise<void> => {
  await apiClient.patch(`${basePath}/products/${id}/status`, { isActive });
};

type PurchaseListParams = {
  search?: string;
  purchaseType?: LicensePurchaseType;
  status?: LicensePurchaseStatus;
  supplierCompanyId?: string;
  pageNumber?: number;
  pageSize?: number;
};

export const getLicensePurchases = async (
  params: PurchaseListParams,
): Promise<PagedResponse<LicensePurchaseListItem>> => {
  const { data } = await apiClient.get<PagedResponse<LicensePurchaseListItem>>(
    `${basePath}/purchases`,
    { params },
  );
  return data;
};

export const getLicensePurchaseById = async (id: string): Promise<LicensePurchaseDetail> => {
  const { data } = await apiClient.get<LicensePurchaseDetail>(`${basePath}/purchases/${id}`);
  return data;
};

export const createLicensePurchase = async (
  request: LicensePurchaseFormRequest & { status: LicensePurchaseStatus },
): Promise<void> => {
  await apiClient.post(`${basePath}/purchases`, request);
};

export const updateLicensePurchase = async (
  id: string,
  request: LicensePurchaseFormRequest,
): Promise<void> => {
  await apiClient.put(`${basePath}/purchases/${id}`, request);
};

export const updateLicensePurchaseStatus = async (
  id: string,
  status: LicensePurchaseStatus,
): Promise<void> => {
  await apiClient.patch(`${basePath}/purchases/${id}/status`, { status });
};

type PackageListParams = {
  search?: string;
  purchaseId?: string;
  productId?: string;
  status?: LicensePackageStatus;
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

export const getLicensePackages = async (
  params: PackageListParams,
): Promise<PagedResponse<LicensePackageListItem>> => {
  const { data } = await apiClient.get<PagedResponse<LicensePackageListItem>>(
    `${basePath}/packages`,
    { params },
  );
  return data;
};

export const getLicensePackageById = async (id: string): Promise<LicensePackageDetail> => {
  const { data } = await apiClient.get<LicensePackageDetail>(`${basePath}/packages/${id}`);
  return data;
};

export const createLicensePackage = async (
  request: LicensePackageFormRequest & { status: LicensePackageStatus },
): Promise<void> => {
  await apiClient.post(`${basePath}/packages`, request);
};

export const updateLicensePackage = async (
  id: string,
  request: LicensePackageFormRequest,
): Promise<void> => {
  await apiClient.put(`${basePath}/packages/${id}`, request);
};

export const updateLicensePackageStatus = async (
  id: string,
  status: LicensePackageStatus,
): Promise<void> => {
  await apiClient.patch(`${basePath}/packages/${id}/status`, { status });
};

export const getAllLicenseCompanies = async (): Promise<LicenseCompanyListItem[]> => {
  const { data } = await apiClient.get<PagedResponse<LicenseCompanyListItem>>(
    `${basePath}/companies`,
    { params: { pageNumber: 1, pageSize: 100, isActive: true } },
  );
  return data.items;
};

export const getAllLicensedProducts = async (): Promise<LicensedProductListItem[]> => {
  const { data } = await apiClient.get<PagedResponse<LicensedProductListItem>>(
    `${basePath}/products`,
    { params: { pageNumber: 1, pageSize: 100, isActive: true } },
  );
  return data.items;
};

export const getAllLicensePurchases = async (): Promise<LicensePurchaseListItem[]> => {
  const { data } = await apiClient.get<PagedResponse<LicensePurchaseListItem>>(
    `${basePath}/purchases`,
    { params: { pageNumber: 1, pageSize: 100 } },
  );
  return data.items;
};

type RequestListParams = {
  search?: string;
  status?: LicenseRequestStatus;
  requestSource?: LicenseRequestSource;
  requestDateFrom?: string;
  requestDateTo?: string;
  requestedByAdObjectId?: string;
  productId?: string;
  pageNumber?: number;
  pageSize?: number;
};

export const getLicenseRequests = async (
  params: RequestListParams,
): Promise<PagedResponse<LicenseRequestListItem>> => {
  const { data } = await apiClient.get<PagedResponse<LicenseRequestListItem>>(
    `${basePath}/requests`,
    { params },
  );
  return data;
};

export const getLicenseRequestById = async (id: string): Promise<LicenseRequestDetail> => {
  const { data } = await apiClient.get<LicenseRequestDetail>(`${basePath}/requests/${id}`);
  return data;
};

export const createLicenseRequest = async (
  request: LicenseRequestFormRequest,
): Promise<LicenseRequestDetail> => {
  const { data } = await apiClient.post<LicenseRequestDetail>(`${basePath}/requests`, request);
  return data;
};

export const updateLicenseRequest = async (
  id: string,
  request: LicenseRequestFormRequest,
): Promise<LicenseRequestDetail> => {
  const { data } = await apiClient.put<LicenseRequestDetail>(`${basePath}/requests/${id}`, request);
  return data;
};

export const updateLicenseRequestStatus = async (
  id: string,
  status: LicenseRequestStatus,
): Promise<LicenseRequestDetail> => {
  const { data } = await apiClient.patch<LicenseRequestDetail>(`${basePath}/requests/${id}/status`, {
    status,
  });
  return data;
};

export const getDirectoryUserLookupReadiness = async (): Promise<DirectoryUserLookupReadiness> => {
  const { data } = await apiClient.get<DirectoryUserLookupReadiness>(
    `${basePath}/directory-user-lookup/readiness`,
  );
  return data;
};
