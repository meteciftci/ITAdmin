import type { LicensePackageStatus, LicensePurchaseStatus } from "./types.ts";

const purchaseTransitions: Record<LicensePurchaseStatus, LicensePurchaseStatus[]> = {
  Draft: ["Draft", "Active", "Cancelled", "Archived"],
  Active: ["Active", "Cancelled", "Archived"],
  Cancelled: ["Cancelled", "Archived"],
  Archived: ["Archived"],
};

const packageTransitions: Record<LicensePackageStatus, LicensePackageStatus[]> = {
  Active: ["Active", "Expired", "Cancelled", "Suspended", "Archived"],
  Suspended: ["Suspended", "Active", "Expired", "Cancelled", "Archived"],
  Expired: ["Expired", "Archived"],
  Cancelled: ["Cancelled", "Archived"],
  Archived: ["Archived"],
};

export function getAllowedPurchaseStatuses(
  current: LicensePurchaseStatus | null,
): LicensePurchaseStatus[] {
  return current ? purchaseTransitions[current] : ["Draft", "Active"];
}

export function getAllowedPackageStatuses(
  current: LicensePackageStatus | null,
): LicensePackageStatus[] {
  return current ? packageTransitions[current] : ["Active", "Expired", "Cancelled", "Suspended", "Archived"];
}

export function isPackageActive(status: LicensePackageStatus): boolean {
  return status === "Active";
}
