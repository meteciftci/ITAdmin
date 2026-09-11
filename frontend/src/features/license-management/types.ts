export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type LicenseManagementSettings = {
  defaultCurrency: string;
  defaultVatIncluded: boolean;
  defaultRenewalReminderDays: number;
  defaultRenewalRecipients: string | null;
  defaultRenewalCcRecipients: string | null;
  lastRenewalReminderRunAt: string | null;
  lastRenewalReminderStatus: string | null;
  lastRenewalReminderDueCount: number;
  lastRenewalReminderQueuedCount: number;
  lastRenewalReminderMessage: string | null;
  notes: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicenseRequestDefaults = Pick<
  LicenseManagementSettings,
  "defaultCurrency" | "defaultVatIncluded"
>;

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
  status: LicensePurchaseStatus;
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
  sensitiveDataVisible: boolean;
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
  status: LicensePackageStatus;
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
  licenseType: LicenseType;
  requestedQuantity: number;
  estimatedUnitCost?: number | null;
  currency?: string | null;
  vatIncluded?: boolean | null;
  justification?: string | null;
  status: LicenseRequestItemStatus;
  users: LicenseRequestItemUserInput[];
};

export type LicenseRequestListItem = {
  id: string;
  requestSource: LicenseRequestSource;
  requestDate: string;
  externalRequestNumber: string | null;
  ebysNumber: string | null;
  requesterUnitDisplayName: string;
  requesterManagerName: string | null;
  productCount: number;
  userCount: number;
  requestedQuantity: number;
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
  licenseType: LicenseType;
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
  requestSource: LicenseRequestSource;
  requestDate: string;
  externalRequestNumber?: string | null;
  ebysNumber?: string | null;
  ebysDate?: string | null;
  requesterUnit: LicenseRequestOuSnapshot;
  requesterManagerName?: string | null;
  description?: string | null;
  estimatedTotalCost?: number | null;
  currency?: string | null;
  vatIncluded?: boolean | null;
  costNote?: string | null;
  items: LicenseRequestItemInput[];
};

// ---- Fulfillment (request -> purchase/package conversion) ----

export type LicenseFulfillmentCandidateUser = {
  id: string;
  adObjectId: string;
  samAccountName: string | null;
  userPrincipalName: string | null;
  displayName: string | null;
  department: string | null;
  title: string | null;
  mail: string | null;
  status: LicenseRequestItemUserStatus;
};

export type LicenseFulfillmentCandidate = {
  requestId: string;
  requestItemId: string;
  requestSource: LicenseRequestSource;
  requestDate: string;
  requesterUnitDisplayName: string;
  productId: string;
  productName: string;
  productBrand: string | null;
  licenseType: LicenseType;
  requestedQuantity: number;
  approvedQuantity: number | null;
  fulfilledQuantity: number;
  remainingQuantity: number;
  itemStatus: LicenseRequestItemStatus;
  isFulfillable: boolean;
  users: LicenseFulfillmentCandidateUser[];
};

export type TriageLicenseRequestItemRequest = {
  requestItemId: string;
  status: LicenseRequestItemStatus;
  approvedQuantity: number | null;
  approvedUserIds?: string[];
};

export type ConvertFulfillmentLine = {
  requestItemId: string;
  fulfillQuantity: number;
  requestItemUserIds?: string[];
};

export type ConvertFulfillmentPackageDefaults = {
  productId: string;
  licenseType: LicenseType;
  startDate: string | null;
  endDate: string | null;
  isPerpetual: boolean;
};

export type ConvertFulfillmentNewPurchase = {
  purchaseType: LicensePurchaseType;
  title: string;
  description: string | null;
  purchaseDate: string | null;
  supplierCompanyId: string | null;
  supportCompanyId: string | null;
  actualTotalCost: number | null;
  currency: string | null;
  vatIncluded: boolean | null;
  notes: string | null;
};

export type ConvertFulfillmentRenewalLine = {
  sourcePackageId: string;
  quantity: number;
  licenseType: LicenseType | null;
  startDate: string | null;
  endDate: string | null;
  isPerpetual: boolean;
  expireSourcePackage: boolean;
  copySeatAssignments: boolean;
  renewalRequired: boolean;
  renewalDate: string | null;
};

export type ConvertFulfillmentManualLine = {
  productId: string;
  quantity: number;
  licenseType: LicenseType;
  startDate: string | null;
  endDate: string | null;
  isPerpetual: boolean;
};

export type ConvertLicenseRequestItemsRequest = {
  existingPurchaseId: string | null;
  newPurchase: ConvertFulfillmentNewPurchase | null;
  lines: ConvertFulfillmentLine[];
  packageDefaults: ConvertFulfillmentPackageDefaults[];
  renewalLines?: ConvertFulfillmentRenewalLine[];
  manualLines?: ConvertFulfillmentManualLine[];
};

export type LicenseFulfillmentResponse = {
  success: boolean;
  message: string;
  purchaseId: string | null;
  packageIds: string[];
};

export type LicenseSeatAssignmentStatus = "Active" | "Released" | "Transferred";

export type LicenseSeatAssignment = {
  id: string;
  packageId: string;
  adObjectId: string | null;
  displayName: string;
  samAccountName: string | null;
  userPrincipalName: string | null;
  mail: string | null;
  nationalId: string | null;
  department: string | null;
  title: string | null;
  assignedDate: string;
  releasedDate: string | null;
  status: LicenseSeatAssignmentStatus;
  replacesAssignmentId: string | null;
  replacesDisplayName: string | null;
  sourceRequestItemId: string | null;
  note: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type LicenseSeatAssignmentListItem = {
  id: string;
  packageId: string;
  productName: string;
  productBrand: string | null;
  purchaseTitle: string;
  displayName: string;
  adObjectId: string | null;
  mail: string | null;
  nationalId: string | null;
  department: string | null;
  assignedDate: string;
  releasedDate: string | null;
  status: LicenseSeatAssignmentStatus;
};

export type LicensePackageSeatOverview = {
  packageId: string;
  productName: string;
  purchaseTitle: string;
  quantity: number;
  activeCount: number;
  availableCount: number;
  assignments: LicenseSeatAssignment[];
};

export type LicenseSeatPersonInput = {
  adObjectId?: string | null;
  displayName: string;
  samAccountName?: string | null;
  userPrincipalName?: string | null;
  mail?: string | null;
  nationalId?: string | null;
  department?: string | null;
  title?: string | null;
};

export type AssignLicenseSeatRequest = {
  person: LicenseSeatPersonInput;
  assignedDate?: string | null;
  sourceRequestItemId?: string | null;
  note?: string | null;
};

export type ReleaseLicenseSeatRequest = {
  releasedDate?: string | null;
  note?: string | null;
};

export type TransferLicenseSeatRequest = {
  newPerson: LicenseSeatPersonInput;
  transferDate?: string | null;
  note?: string | null;
};

export type LicenseSeatAssignmentOperationResponse = {
  success: boolean;
  message: string;
  assignment: LicenseSeatAssignment | null;
};

export type CopyLicenseSeatsResponse = {
  success: boolean;
  message: string;
  copiedCount: number;
  skippedCount: number;
};
