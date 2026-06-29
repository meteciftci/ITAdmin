export const LICENSE_PURCHASE_DETAIL_PATH_PREFIX = "/license-management/purchases";

export function buildLicensePurchaseDetailPath(purchaseId: string): string {
  return `${LICENSE_PURCHASE_DETAIL_PATH_PREFIX}/${purchaseId}`;
}

export function buildLicensePurchaseEditPath(purchaseId: string): string {
  return `${LICENSE_PURCHASE_DETAIL_PATH_PREFIX}/${purchaseId}/edit`;
}

export const LICENSE_PURCHASE_CREATE_PATH = "/license-management/purchases/create";
