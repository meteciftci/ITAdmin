import { AD_ORGANIZATIONAL_UNITS_LIST_PATH } from "./ad-ous-list-path.ts";

export function buildAdOrganizationalUnitDetailPath(organizationalUnitId: string): string {
  return `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/${organizationalUnitId}`;
}
