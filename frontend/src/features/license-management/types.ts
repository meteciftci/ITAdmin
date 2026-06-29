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
  purchaseCount: number;
  packageCount: number;
  totalLicenseQuantity: number;
};

export type LicenseManagementSettings = {
  defaultCurrency: string;
  defaultVatIncluded: boolean;
  defaultRenewalReminderDays: number;
  defaultRenewalRecipients: string | null;
  defaultRenewalCcRecipients: string | null;
  notes: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type UpdateLicenseManagementSettingsRequest = {
  defaultCurrency: string;
  defaultVatIncluded: boolean;
  defaultRenewalReminderDays: number;
  defaultRenewalRecipients?: string | null;
  defaultRenewalCcRecipients?: string | null;
  notes?: string | null;
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

export type LicensePurchaseType =
  | "LegacyPerpetual"
  | "Tender"
  | "DirectPurchase"
  | "Dmo"
  | "Renewal"
  | "CorporateSubscription"
  | "Other";

export type LicensePurchaseStatus = "Draft" | "Active" | "Cancelled" | "Archived";

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

export type LicensePurchaseListItem = {
  id: string;
  title: string;
  purchaseType: LicensePurchaseType;
  purchaseDate: string | null;
  supplierCompanyName: string | null;
  supportCompanyName: string | null;
  contractNumber: string | null;
  status: LicensePurchaseStatus;
};

export type LicensePurchaseDetail = {
  id: string;
  purchaseType: LicensePurchaseType;
  title: string;
  description: string | null;
  purchaseDate: string | null;
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
  status: LicensePurchaseStatus;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicensePurchaseFormRequest = {
  purchaseType: LicensePurchaseType;
  title: string;
  description?: string | null;
  purchaseDate?: string | null;
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
  status?: LicensePurchaseStatus;
};

export type LicensePackageListItem = {
  id: string;
  productName: string;
  purchaseTitle: string;
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
  purchaseId: string;
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
  purchaseId: string;
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
