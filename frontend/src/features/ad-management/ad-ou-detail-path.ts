import { AD_ORGANIZATIONAL_UNITS_LIST_PATH } from "./ad-ous-list-path.ts";

export const AD_ORGANIZATIONAL_UNIT_CREATE_PATH = `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/create`;

export function buildAdOrganizationalUnitDetailPath(organizationalUnitId: string): string {
  return `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/${organizationalUnitId}`;
}

export function buildAdOrganizationalUnitCreatePath(
  parentDistinguishedName?: string | null,
): string {
  if (!parentDistinguishedName?.trim()) {
    return AD_ORGANIZATIONAL_UNIT_CREATE_PATH;
  }

  const params = new URLSearchParams({ parentDn: parentDistinguishedName.trim() });
  return `${AD_ORGANIZATIONAL_UNIT_CREATE_PATH}?${params.toString()}`;
}

export function buildAdOrganizationalUnitRenamePath(organizationalUnitId: string): string {
  return `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/${organizationalUnitId}/rename`;
}

export function buildAdOrganizationalUnitMovePath(organizationalUnitId: string): string {
  return `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/${organizationalUnitId}/move`;
}

export function readAdOrganizationalUnitCreateParentDn(
  searchParams: URLSearchParams,
): string | null {
  const parentDn = searchParams.get("parentDn")?.trim();
  return parentDn || null;
}
