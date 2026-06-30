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
  contactPersonName: string | null;
  contactPersonPhone: string | null;
  isActive: boolean;
};

export type LicenseCompanyDetail = LicenseCompanyListItem & {
  website: string | null;
  contactPersonEmail: string | null;
  notes: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicenseCompanyFormRequest = {
  name: string;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  contactPersonName?: string | null;
  contactPersonPhone?: string | null;
  contactPersonEmail?: string | null;
  notes?: string | null;
  isActive: boolean;
};

export type LicenseProductCategoryListItem = {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
};

export type LicenseProductCategoryDetail = LicenseProductCategoryListItem & {
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicenseProductCategoryFormRequest = {
  name: string;
  description?: string | null;
  isActive: boolean;
};

export type LicensedProductListItem = {
  id: string;
  name: string;
  brand: string | null;
  categoryId: string;
  categoryName: string;
  isActive: boolean;
};

export type LicensedProductDetail = LicensedProductListItem & {
  description: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicensedProductFormRequest = {
  name: string;
  brand?: string | null;
  categoryId: string;
  description?: string | null;
  isActive: boolean;
};

export type DirectoryUserLookupReadiness = {
  isReady: boolean;
  reason: string;
  message: string | null;
};

export type DirectoryOrganizationalUnitLookupItem = {
  objectGuid: string;
  displayName: string;
  name: string | null;
  distinguishedName: string;
};

export type DirectoryOrganizationalUnitLookupSearchResult = {
  isSuccess: boolean;
  message: string | null;
  items: DirectoryOrganizationalUnitLookupItem[];
};

export type LicenseRequestOuSnapshot = {
  objectGuid: string;
  displayName: string;
  distinguishedName: string;
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

export type LicenseRequestSource =
  | "OfficialLetter"
  | "CorporateRequestSystem"
  | "Email"
  | "VerbalInstruction"
  | "Other";

export type LicenseRequestStatus =
  | "Draft"
  | "Pending"
  | "InReview"
  | "PartiallyFulfilled"
  | "Fulfilled"
  | "Rejected"
  | "Cancelled"
  | "Archived";

export type LicenseRequestItemStatus =
  | "Pending"
  | "InReview"
  | "Approved"
  | "Rejected"
  | "PartiallyFulfilled"
  | "Fulfilled"
  | "Cancelled";

export type LicenseRequestItemUserStatus =
  | "Pending"
  | "Approved"
  | "Rejected"
  | "Fulfilled"
  | "Cancelled";

export type LicenseRequestAdUserSnapshot = {
  adObjectId: string;
  samAccountName?: string | null;
  userPrincipalName?: string | null;
  displayName?: string | null;
  department?: string | null;
  title?: string | null;
  mail?: string | null;
  phone?: string | null;
};

export type LicenseRequestItemUserInput = LicenseRequestAdUserSnapshot & {
  status: LicenseRequestItemUserStatus;
};

export type LicenseRequestItemInput = {
  productId: string;
  estimatedUnitCost?: number | null;
  currency?: string | null;
  vatIncluded?: boolean | null;
  justification?: string | null;
  status: LicenseRequestItemStatus;
  users: LicenseRequestItemUserInput[];
};

export type LicenseRequestListItem = {
  id: string;
  requestNumber: string;
  requestSource: LicenseRequestSource;
  requestDate: string;
  requesterUnitDisplayName: string;
  requesterManagerName: string | null;
  productCount: number;
  userCount: number;
  estimatedTotalCost: number | null;
  currency: string | null;
  status: LicenseRequestStatus;
};

export type LicenseRequestItemUserDetail = LicenseRequestAdUserSnapshot & {
  id: string;
  status: LicenseRequestItemUserStatus;
};

export type LicenseRequestItemDetail = {
  id: string;
  productId: string;
  productName: string;
  requestedQuantity: number;
  approvedQuantity: number | null;
  fulfilledQuantity: number;
  estimatedUnitCost: number | null;
  estimatedTotalCost: number | null;
  currency: string | null;
  vatIncluded: boolean | null;
  justification: string | null;
  status: LicenseRequestItemStatus;
  users: LicenseRequestItemUserDetail[];
};

export type LicenseRequestDetail = {
  id: string;
  requestNumber: string;
  requestSource: LicenseRequestSource;
  requestDate: string;
  externalRequestNumber: string | null;
  ebysNumber: string | null;
  ebysDate: string | null;
  requesterUnitDisplayName: string;
  requesterUnitDistinguishedName: string;
  requesterUnitObjectGuid: string;
  requesterManagerName: string | null;
  description: string | null;
  status: LicenseRequestStatus;
  estimatedTotalCost: number | null;
  currency: string | null;
  vatIncluded: boolean | null;
  costNote: string | null;
  isActive: boolean;
  items: LicenseRequestItemDetail[];
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicenseRequestFormRequest = {
  requestNumber: string;
  requestSource: LicenseRequestSource;
  requestDate: string;
  externalRequestNumber?: string | null;
  ebysNumber?: string | null;
  ebysDate?: string | null;
  requesterUnit: LicenseRequestOuSnapshot;
  requesterManagerName?: string | null;
  description?: string | null;
  status: LicenseRequestStatus;
  estimatedTotalCost?: number | null;
  currency?: string | null;
  vatIncluded?: boolean | null;
  costNote?: string | null;
  items: LicenseRequestItemInput[];
};
