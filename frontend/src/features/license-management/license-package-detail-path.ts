export const LICENSE_PACKAGE_DETAIL_PATH_PREFIX = "/license-management/packages";

export function buildLicensePackageDetailPath(packageId: string): string {
  return `${LICENSE_PACKAGE_DETAIL_PATH_PREFIX}/${packageId}`;
}

export function buildLicensePackageEditPath(packageId: string): string {
  return `${LICENSE_PACKAGE_DETAIL_PATH_PREFIX}/${packageId}/edit`;
}

export const LICENSE_PACKAGE_CREATE_PATH = "/license-management/packages/create";

export function buildLicensePackageCreatePath(purchaseId?: string): string {
  if (!purchaseId) {
    return LICENSE_PACKAGE_CREATE_PATH;
  }

  const params = new URLSearchParams({ purchaseId });
  return `${LICENSE_PACKAGE_CREATE_PATH}?${params.toString()}`;
}
