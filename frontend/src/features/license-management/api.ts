import { apiClient } from "@/lib/api-client";

import type {
  LicenseAcquisitionDetail,
  LicenseAcquisitionFormRequest,
  LicenseAcquisitionListItem,
  LicenseAcquisitionStatus,
  LicenseAcquisitionType,
  LicenseCompanyDetail,
  LicenseCompanyFormRequest,
  LicenseCompanyListItem,
  LicensedProductDetail,
  LicensedProductFormRequest,
  LicensedProductListItem,
  LicenseManagementOverview,
  LicensePackageDetail,
  LicensePackageFormRequest,
  LicensePackageListItem,
  LicensePackageStatus,
  PagedResponse,
} from "@/features/license-management/types";

const basePath = "/license-management";

export const getLicenseManagementOverview = async (): Promise<LicenseManagementOverview> => {
  const { data } = await apiClient.get<LicenseManagementOverview>(`${basePath}/overview`);
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

type ProductListParams = {
  search?: string;
  isActive?: boolean;
  vendorCompanyId?: string;
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

type AcquisitionListParams = {
  search?: string;
  acquisitionType?: LicenseAcquisitionType;
  status?: LicenseAcquisitionStatus;
  supplierCompanyId?: string;
  pageNumber?: number;
  pageSize?: number;
};

export const getLicenseAcquisitions = async (
  params: AcquisitionListParams,
): Promise<PagedResponse<LicenseAcquisitionListItem>> => {
  const { data } = await apiClient.get<PagedResponse<LicenseAcquisitionListItem>>(
    `${basePath}/acquisitions`,
    { params },
  );
  return data;
};

export const getLicenseAcquisitionById = async (
  id: string,
): Promise<LicenseAcquisitionDetail> => {
  const { data } = await apiClient.get<LicenseAcquisitionDetail>(
    `${basePath}/acquisitions/${id}`,
  );
  return data;
};

export const createLicenseAcquisition = async (
  request: LicenseAcquisitionFormRequest & { status: LicenseAcquisitionStatus },
): Promise<void> => {
  await apiClient.post(`${basePath}/acquisitions`, request);
};

export const updateLicenseAcquisition = async (
  id: string,
  request: LicenseAcquisitionFormRequest,
): Promise<void> => {
  await apiClient.put(`${basePath}/acquisitions/${id}`, request);
};

export const updateLicenseAcquisitionStatus = async (
  id: string,
  status: LicenseAcquisitionStatus,
): Promise<void> => {
  await apiClient.patch(`${basePath}/acquisitions/${id}/status`, { status });
};

type PackageListParams = {
  search?: string;
  acquisitionId?: string;
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

export const getAllLicenseAcquisitions = async (): Promise<LicenseAcquisitionListItem[]> => {
  const { data } = await apiClient.get<PagedResponse<LicenseAcquisitionListItem>>(
    `${basePath}/acquisitions`,
    { params: { pageNumber: 1, pageSize: 100 } },
  );
  return data.items;
};
