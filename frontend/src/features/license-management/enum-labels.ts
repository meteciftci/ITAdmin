import type { TFunction } from "i18next";

import type {
  LicensePackageStatus,
  LicensePurchaseStatus,
  LicensePurchaseType,
  LicenseRequestItemStatus,
  LicenseRequestItemUserStatus,
  LicenseRequestSource,
  LicenseRequestStatus,
  LicenseType,
} from "@/features/license-management/types";

export const PURCHASE_TYPES: LicensePurchaseType[] = [
  "LegacyPerpetual",
  "Tender",
  "DirectPurchase",
  "Dmo",
  "Renewal",
  "CorporateSubscription",
  "Other",
];

export const PURCHASE_STATUSES: LicensePurchaseStatus[] = [
  "Draft",
  "Active",
  "Cancelled",
  "Archived",
];

export const LICENSE_TYPES: LicenseType[] = [
  "NamedUser",
  "Concurrent",
  "DeviceBased",
  "ServerBased",
  "SiteLicense",
  "Subscription",
  "Perpetual",
  "Trial",
  "Free",
  "Other",
];

export const PACKAGE_STATUSES: LicensePackageStatus[] = [
  "Active",
  "Expired",
  "Cancelled",
  "Suspended",
  "Archived",
];

export const REQUEST_SOURCES: LicenseRequestSource[] = [
  "OfficialLetter",
  "CorporateRequestSystem",
  "Email",
  "VerbalInstruction",
  "Other",
];

export const REQUEST_STATUSES: LicenseRequestStatus[] = [
  "Draft",
  "Pending",
  "InReview",
  "PartiallyFulfilled",
  "Fulfilled",
  "Rejected",
  "Cancelled",
  "Archived",
];

export const MANUAL_REQUEST_STATUSES: LicenseRequestStatus[] = [
  "Draft",
  "Pending",
  "InReview",
  "Rejected",
  "Cancelled",
  "Archived",
];

export const REQUEST_ITEM_STATUSES: LicenseRequestItemStatus[] = [
  "Pending",
  "InReview",
  "Approved",
  "Rejected",
  "PartiallyFulfilled",
  "Fulfilled",
  "Cancelled",
];

export const MANUAL_REQUEST_ITEM_STATUSES: LicenseRequestItemStatus[] = [
  "Pending",
  "InReview",
  "Approved",
  "Rejected",
  "Cancelled",
];

export const REQUEST_ITEM_USER_STATUSES: LicenseRequestItemUserStatus[] = [
  "Pending",
  "Approved",
  "Rejected",
  "Fulfilled",
  "Cancelled",
];

export const MANUAL_REQUEST_ITEM_USER_STATUSES: LicenseRequestItemUserStatus[] = [
  "Pending",
  "Approved",
  "Rejected",
  "Cancelled",
];

export function getPurchaseTypeLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicensePurchaseType,
): string {
  return t(`licenseManagement:enums.purchaseType.${value}`);
}

export function getPurchaseStatusLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicensePurchaseStatus,
): string {
  return t(`licenseManagement:enums.purchaseStatus.${value}`);
}

export function getLicenseTypeLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicenseType,
): string {
  return t(`licenseManagement:enums.licenseType.${value}`);
}

export function getPackageStatusLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicensePackageStatus,
): string {
  return t(`licenseManagement:enums.packageStatus.${value}`);
}

export function getRequestSourceLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicenseRequestSource,
): string {
  return t(`licenseManagement:enums.requestSource.${value}`);
}

export function getRequestStatusLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicenseRequestStatus,
): string {
  return t(`licenseManagement:enums.requestStatus.${value}`);
}

export function getRequestItemStatusLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicenseRequestItemStatus,
): string {
  return t(`licenseManagement:enums.requestItemStatus.${value}`);
}

export function getRequestItemUserStatusLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicenseRequestItemUserStatus,
): string {
  return t(`licenseManagement:enums.requestItemUserStatus.${value}`);
}

export function maskLicenseKey(value: string | null | undefined): string {
  if (!value) {
    return "-";
  }

  if (value.length <= 8) {
    return "••••••••";
  }

  return `${value.slice(0, 4)}••••${value.slice(-4)}`;
}
