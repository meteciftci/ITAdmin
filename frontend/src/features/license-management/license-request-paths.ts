export const LICENSE_REQUESTS_LIST_PATH = "/license-management/requests";
export const LICENSE_REQUEST_CREATE_PATH = "/license-management/requests/create";
export const LICENSE_FULFILLMENT_PATH = "/license-management/requests/fulfillment";

export function buildLicenseRequestDetailPath(id: string): string {
  return `/license-management/requests/${id}`;
}

export function buildLicenseRequestEditPath(id: string): string {
  return `/license-management/requests/${id}/edit`;
}
