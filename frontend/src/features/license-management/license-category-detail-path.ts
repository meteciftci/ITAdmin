export const LICENSE_CATEGORY_DETAIL_PATH_PREFIX = "/license-management/categories";

export function buildLicenseCategoryDetailPath(categoryId: string): string {
  return `${LICENSE_CATEGORY_DETAIL_PATH_PREFIX}/${categoryId}`;
}

export function buildLicenseCategoryEditPath(categoryId: string): string {
  return `${LICENSE_CATEGORY_DETAIL_PATH_PREFIX}/${categoryId}/edit`;
}

export const LICENSE_CATEGORY_CREATE_PATH = "/license-management/categories/create";
