import type { TFunction } from "i18next";

import type { LicenseRequestItemDraft } from "@/features/license-management/license-request-payload";
import type { LicenseRequestAdUserSnapshot } from "@/features/license-management/types";

export type LicenseRequestFormValidationResult =
  | { isValid: true }
  | { isValid: false; message: string };

export function validateLicenseRequestForm(
  t: TFunction<["licenseManagement", "common"]>,
  input: {
    requestNumber: string;
    requestDate: string | null;
    requestedBy: LicenseRequestAdUserSnapshot | null;
    items: LicenseRequestItemDraft[];
  },
): LicenseRequestFormValidationResult {
  if (!input.requestNumber.trim()) {
    return { isValid: false, message: t("licenseManagement:requests.validation.requestNumberRequired") };
  }

  if (!input.requestDate) {
    return { isValid: false, message: t("licenseManagement:requests.validation.requestDateRequired") };
  }

  if (!input.requestedBy?.adObjectId) {
    return { isValid: false, message: t("licenseManagement:requests.validation.requestedByRequired") };
  }

  if (input.items.length === 0) {
    return { isValid: false, message: t("licenseManagement:requests.validation.itemsRequired") };
  }

  const productIds = new Set<string>();
  for (const item of input.items) {
    if (!item.productId) {
      return { isValid: false, message: t("licenseManagement:requests.validation.productRequired") };
    }

    if (productIds.has(item.productId)) {
      return { isValid: false, message: t("licenseManagement:requests.validation.duplicateProduct") };
    }

    productIds.add(item.productId);

    if (item.users.length === 0) {
      return { isValid: false, message: t("licenseManagement:requests.validation.usersRequired") };
    }

    const userIds = new Set<string>();
    for (const user of item.users) {
      if (userIds.has(user.adObjectId)) {
        return { isValid: false, message: t("licenseManagement:requests.validation.duplicateUser") };
      }

      userIds.add(user.adObjectId);
    }

    const unitCost = item.estimatedUnitCost.trim();
    if (unitCost && Number(unitCost.replace(",", ".")) < 0) {
      return { isValid: false, message: t("licenseManagement:requests.validation.negativeCost") };
    }
  }

  return { isValid: true };
}
