export const LICENSE_PRODUCT_DETAIL_PATH_PREFIX = "/license-management/products";

export function buildLicenseProductDetailPath(productId: string): string {
  return `${LICENSE_PRODUCT_DETAIL_PATH_PREFIX}/${productId}`;
}

export function buildLicenseProductEditPath(productId: string): string {
  return `${LICENSE_PRODUCT_DETAIL_PATH_PREFIX}/${productId}/edit`;
}

export const LICENSE_PRODUCT_CREATE_PATH = "/license-management/products/create";
