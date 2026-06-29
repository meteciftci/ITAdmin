import type { TFunction } from "i18next";

import type {
  LicensePackageStatus,
  LicensePurchaseStatus,
  LicensePurchaseType,
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

export function maskLicenseKey(value: string | null | undefined): string {
  if (!value) {
    return "-";
  }

  if (value.length <= 8) {
    return "••••••••";
  }

  return `${value.slice(0, 4)}••••${value.slice(-4)}`;
}
