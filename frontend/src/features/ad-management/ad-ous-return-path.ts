import { AD_ORGANIZATIONAL_UNITS_LIST_PATH } from "./ad-ous-list-path.ts";

export const AD_ORGANIZATIONAL_UNITS_RETURN_STATE_KEY = "adOrganizationalUnitsReturnPath";

export function buildAdOrganizationalUnitsListReturnState() {
  return {
    [AD_ORGANIZATIONAL_UNITS_RETURN_STATE_KEY]: AD_ORGANIZATIONAL_UNITS_LIST_PATH,
  };
}

export function resolveAdOrganizationalUnitsReturnPath(
  locationState: unknown,
  fallback = AD_ORGANIZATIONAL_UNITS_LIST_PATH,
): string {
  if (!locationState || typeof locationState !== "object") {
    return fallback;
  }

  const value = (locationState as Record<string, unknown>)[AD_ORGANIZATIONAL_UNITS_RETURN_STATE_KEY];
  if (typeof value !== "string" || !value.startsWith(AD_ORGANIZATIONAL_UNITS_LIST_PATH)) {
    return fallback;
  }

  return value;
}
