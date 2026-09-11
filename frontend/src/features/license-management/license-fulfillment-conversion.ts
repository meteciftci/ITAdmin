import type {
  ConvertFulfillmentLine,
  ConvertFulfillmentManualLine,
  ConvertFulfillmentNewPurchase,
  ConvertFulfillmentPackageDefaults,
  ConvertFulfillmentRenewalLine,
  ConvertLicenseRequestItemsRequest,
  LicenseFulfillmentCandidate,
} from "./types.ts";

/** A candidate line the user chose to fulfill, with the quantity to fulfill now. */
export type FulfillmentSelectionLine = {
  candidate: LicenseFulfillmentCandidate;
  fulfillQuantity: number;
  requestItemUserIds?: string[];
};

/** Aggregated view used by the wizard: one row per product (packages are created per product). */
export type ProductGroupSummary = {
  groupKey: string;
  productId: string;
  productName: string;
  licenseType: LicenseFulfillmentCandidate["licenseType"];
  lineCount: number;
  totalQuantity: number;
};

export function summarizeByProduct(lines: FulfillmentSelectionLine[]): ProductGroupSummary[] {
  const map = new Map<string, ProductGroupSummary>();
  for (const { candidate, fulfillQuantity } of lines) {
    const groupKey = `${candidate.productId}:${candidate.licenseType}`;
    const existing = map.get(groupKey);
    if (existing) {
      existing.lineCount += 1;
      existing.totalQuantity += fulfillQuantity;
    } else {
      map.set(groupKey, {
        groupKey,
        productId: candidate.productId,
        productName: candidate.productName,
        licenseType: candidate.licenseType,
        lineCount: 1,
        totalQuantity: fulfillQuantity,
      });
    }
  }
  return [...map.values()];
}

/** Keeps a chosen fulfill quantity within [1, remaining]. */
export function clampFulfillQuantity(value: number, remaining: number): number {
  if (!Number.isFinite(value) || value < 1) {
    return remaining >= 1 ? 1 : 0;
  }
  return Math.min(Math.floor(value), remaining);
}

export type ConvertSelectionValidation =
  | { isValid: true }
  | { isValid: false; messageKey: string };

export function validateSelection(lines: FulfillmentSelectionLine[]): ConvertSelectionValidation {
  if (lines.length === 0) {
    return { isValid: false, messageKey: "requests.fulfillment.validation.noLines" };
  }

  for (const { candidate, fulfillQuantity, requestItemUserIds } of lines) {
    if (fulfillQuantity < 1 || fulfillQuantity > candidate.remainingQuantity) {
      return { isValid: false, messageKey: "requests.fulfillment.validation.quantityRange" };
    }

    if (candidate.licenseType === "NamedUser") {
      const uniqueUserIds = new Set(requestItemUserIds ?? []);
      const approvedIds = new Set(
        candidate.users.filter((user) => user.status === "Approved").map((user) => user.id),
      );
      if (
        uniqueUserIds.size !== fulfillQuantity
        || [...uniqueUserIds].some((id) => !approvedIds.has(id))
      ) {
        return { isValid: false, messageKey: "requests.fulfillment.validation.namedUsersRequired" };
      }
    } else if ((requestItemUserIds?.length ?? 0) > 0) {
      return { isValid: false, messageKey: "requests.fulfillment.validation.usersNotAllowed" };
    }
  }

  return { isValid: true };
}

export type ConvertTarget =
  | { kind: "new"; purchase: ConvertFulfillmentNewPurchase }
  | { kind: "existing"; purchaseId: string };

/** The distinct products in the selection — the wizard collects package defaults for each. */
export function distinctProductIds(lines: FulfillmentSelectionLine[]): string[] {
  return [...new Set(lines.map((line) => line.candidate.productId))];
}

export function buildConvertPayload(
  lines: FulfillmentSelectionLine[],
  target: ConvertTarget,
  packageDefaults: ConvertFulfillmentPackageDefaults[],
  renewalLines: ConvertFulfillmentRenewalLine[] = [],
  manualLines: ConvertFulfillmentManualLine[] = [],
): ConvertLicenseRequestItemsRequest {
  return {
    existingPurchaseId: target.kind === "existing" ? target.purchaseId : null,
    newPurchase: target.kind === "new" ? target.purchase : null,
    lines: lines.map(
      (line): ConvertFulfillmentLine => ({
        requestItemId: line.candidate.requestItemId,
        fulfillQuantity: line.fulfillQuantity,
        ...(line.candidate.licenseType === "NamedUser"
          ? { requestItemUserIds: line.requestItemUserIds ?? [] }
          : {}),
      }),
    ),
    packageDefaults,
    renewalLines: renewalLines.length > 0 ? renewalLines : undefined,
    manualLines: manualLines.length > 0 ? manualLines : undefined,
  };
}
