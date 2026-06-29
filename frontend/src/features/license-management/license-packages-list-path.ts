export const LICENSE_PACKAGES_LIST_PATH = "/license-management/packages";

export function buildLicensePackagesListPath(purchaseId?: string): string {
  if (!purchaseId) {
    return LICENSE_PACKAGES_LIST_PATH;
  }

  const params = new URLSearchParams({ purchaseId });
  return `${LICENSE_PACKAGES_LIST_PATH}?${params.toString()}`;
}
