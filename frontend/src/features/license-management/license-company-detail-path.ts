export const LICENSE_COMPANY_DETAIL_PATH_PREFIX = "/license-management/companies";

export function buildLicenseCompanyDetailPath(companyId: string): string {
  return `${LICENSE_COMPANY_DETAIL_PATH_PREFIX}/${companyId}`;
}

export function buildLicenseCompanyEditPath(companyId: string): string {
  return `${LICENSE_COMPANY_DETAIL_PATH_PREFIX}/${companyId}/edit`;
}

export const LICENSE_COMPANY_CREATE_PATH = "/license-management/companies/create";
