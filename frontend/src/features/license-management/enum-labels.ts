import type { TFunction } from "i18next";

import type {
  LicenseAcquisitionStatus,
  LicenseAcquisitionType,
  LicensePackageStatus,
  LicenseType,
} from "@/features/license-management/types";

export const ACQUISITION_TYPES: LicenseAcquisitionType[] = [
  "LegacyPerpetual",
  "Tender",
  "DirectPurchase",
  "Dmo",
  "Renewal",
  "CorporateSubscription",
  "Other",
];

export const ACQUISITION_STATUSES: LicenseAcquisitionStatus[] = [
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

export function getAcquisitionTypeLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicenseAcquisitionType,
): string {
  return t(`licenseManagement:enums.acquisitionType.${value}`);
}

export function getAcquisitionStatusLabel(
  t: TFunction<["licenseManagement", "common"]>,
  value: LicenseAcquisitionStatus,
): string {
  return t(`licenseManagement:enums.acquisitionStatus.${value}`);
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
