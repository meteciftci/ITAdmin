import type { TFunction } from "i18next";

import type { LicenseRequestItemDraft } from "./license-request-payload.ts";
import type {
  LicenseRequestOuSnapshot,
  LicenseRequestSource,
} from "./types.ts";
import { isRequestSourceFieldVisible } from "./request-source-fields.ts";

export type LicenseRequestFormValidationResult =
  | { isValid: true }
  | { isValid: false; message: string };

export function validateLicenseRequestForm(
  t: TFunction<["licenseManagement", "common"]>,
  input: {
    requestDate: string | null;
    requestSource: LicenseRequestSource;
    requesterUnit: LicenseRequestOuSnapshot | null;
    externalRequestNumber: string;
    ebysNumber: string;
    ebysDate: string | null;
    items: LicenseRequestItemDraft[];
  },
): LicenseRequestFormValidationResult {
  if (!input.requestDate) {
    return { isValid: false, message: t("licenseManagement:requests.validation.requestDateRequired") };
  }

  if (!input.requesterUnit?.objectGuid || !input.requesterUnit.displayName || !input.requesterUnit.distinguishedName) {
    return { isValid: false, message: t("licenseManagement:requests.validation.requesterUnitRequired") };
  }

  if (isRequestSourceFieldVisible("externalRequestNumber", input.requestSource) && !input.externalRequestNumber.trim()) {
    return { isValid: false, message: t("licenseManagement:requests.validation.externalRequestNumberRequired") };
  }

  if (isRequestSourceFieldVisible("ebysNumber", input.requestSource) && !input.ebysNumber.trim()) {
    return { isValid: false, message: t("licenseManagement:requests.validation.ebysNumberRequired") };
  }

  if (isRequestSourceFieldVisible("ebysDate", input.requestSource) && !input.ebysDate) {
    return { isValid: false, message: t("licenseManagement:requests.validation.ebysDateRequired") };
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

    if (item.licenseType === "NamedUser" && item.users.length === 0) {
      return { isValid: false, message: t("licenseManagement:requests.validation.usersRequired") };
    }

    const requestedQuantity = item.licenseType === "NamedUser"
      ? item.users.length
      : Number(item.requestedQuantity);
    if (!Number.isInteger(requestedQuantity) || requestedQuantity < 1) {
      return { isValid: false, message: t("licenseManagement:requests.validation.quantityRequired") };
    }

    if (item.licenseType !== "NamedUser" && item.users.length > 0) {
      return { isValid: false, message: t("licenseManagement:requests.validation.quantityUsersNotAllowed") };
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
