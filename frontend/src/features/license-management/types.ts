export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type LicenseManagementOverview = {
  companyCount: number;
  activeProductCount: number;
  acquisitionCount: number;
  packageCount: number;
  totalLicenseQuantity: number;
};

export type LicenseCompanyListItem = {
  id: string;
  name: string;
  email: string | null;
  phone: string | null;
  supportEmail: string | null;
  contactPersonName: string | null;
  isActive: boolean;
};

export type LicenseCompanyDetail = LicenseCompanyListItem & {
  taxNumber: string | null;
  website: string | null;
  address: string | null;
  supportPhone: string | null;
  contactPersonPhone: string | null;
  contactPersonEmail: string | null;
  notes: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicenseCompanyFormRequest = {
  name: string;
  taxNumber?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  supportPhone?: string | null;
  supportEmail?: string | null;
  contactPersonName?: string | null;
  contactPersonPhone?: string | null;
  contactPersonEmail?: string | null;
  notes?: string | null;
  isActive: boolean;
};

export type LicensedProductListItem = {
  id: string;
  name: string;
  vendorCompanyName: string | null;
  category: string | null;
  defaultLicenseType: LicenseType | null;
  isActive: boolean;
};

export type LicensedProductDetail = LicensedProductListItem & {
  vendorCompanyId: string | null;
  description: string | null;
  notes: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicensedProductFormRequest = {
  name: string;
  vendorCompanyId?: string | null;
  category?: string | null;
  defaultLicenseType?: LicenseType | null;
  description?: string | null;
  isActive: boolean;
  notes?: string | null;
};

export type LicenseAcquisitionType =
  | "LegacyPerpetual"
  | "Tender"
  | "DirectPurchase"
  | "Dmo"
  | "Renewal"
  | "CorporateSubscription"
  | "Other";

export type LicenseAcquisitionStatus = "Draft" | "Active" | "Cancelled" | "Archived";

export type LicenseType =
  | "NamedUser"
  | "Concurrent"
  | "DeviceBased"
  | "ServerBased"
  | "SiteLicense"
  | "Subscription"
  | "Perpetual"
  | "Trial"
  | "Free"
  | "Other";

export type LicensePackageStatus =
  | "Active"
  | "Expired"
  | "Cancelled"
  | "Suspended"
  | "Archived";

export type LicenseAcquisitionListItem = {
  id: string;
  title: string;
  acquisitionType: LicenseAcquisitionType;
  acquisitionDate: string | null;
  supplierCompanyName: string | null;
  supportCompanyName: string | null;
  contractNumber: string | null;
  status: LicenseAcquisitionStatus;
};

export type LicenseAcquisitionDetail = {
  id: string;
  acquisitionType: LicenseAcquisitionType;
  title: string;
  description: string | null;
  acquisitionDate: string | null;
  tenderNumber: string | null;
  tenderDate: string | null;
  directPurchaseNumber: string | null;
  dmoOrderNumber: string | null;
  ebysNumber: string | null;
  ebysDate: string | null;
  invoiceNumber: string | null;
  invoiceDate: string | null;
  contractNumber: string | null;
  contractStartDate: string | null;
  contractEndDate: string | null;
  supplierCompanyId: string | null;
  supplierCompanyName: string | null;
  supportCompanyId: string | null;
  supportCompanyName: string | null;
  actualTotalCost: number | null;
  currency: string | null;
  vatIncluded: boolean | null;
  notes: string | null;
  status: LicenseAcquisitionStatus;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicenseAcquisitionFormRequest = {
  acquisitionType: LicenseAcquisitionType;
  title: string;
  description?: string | null;
  acquisitionDate?: string | null;
  tenderNumber?: string | null;
  tenderDate?: string | null;
  directPurchaseNumber?: string | null;
  dmoOrderNumber?: string | null;
  ebysNumber?: string | null;
  ebysDate?: string | null;
  invoiceNumber?: string | null;
  invoiceDate?: string | null;
  contractNumber?: string | null;
  contractStartDate?: string | null;
  contractEndDate?: string | null;
  supplierCompanyId?: string | null;
  supportCompanyId?: string | null;
  actualTotalCost?: number | null;
  currency?: string | null;
  vatIncluded?: boolean | null;
  notes?: string | null;
  status?: LicenseAcquisitionStatus;
};

export type LicensePackageListItem = {
  id: string;
  productName: string;
  acquisitionTitle: string;
  licenseType: LicenseType;
  quantity: number;
  usedQuantity: number;
  availableQuantity: number;
  startDate: string | null;
  endDate: string | null;
  isPerpetual: boolean;
  renewalRequired: boolean;
  status: LicensePackageStatus;
  isActive: boolean;
};

export type LicensePackageDetail = LicensePackageListItem & {
  acquisitionId: string;
  productId: string;
  renewalDate: string | null;
  serialNumber: string | null;
  licenseKey: string | null;
  licenseAccountEmail: string | null;
  licensePortalUrl: string | null;
  licenseNotes: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicensePackageFormRequest = {
  acquisitionId: string;
  productId: string;
  licenseType: LicenseType;
  quantity: number;
  startDate?: string | null;
  endDate?: string | null;
  isPerpetual: boolean;
  renewalRequired: boolean;
  renewalDate?: string | null;
  serialNumber?: string | null;
  licenseKey?: string | null;
  licenseAccountEmail?: string | null;
  licensePortalUrl?: string | null;
  licenseNotes?: string | null;
  isActive: boolean;
  status?: LicensePackageStatus;
};
