import { buildAdGroupDetailPath } from "./ad-group-detail-path.ts";
import { AD_GROUPS_LIST_PATH } from "./ad-groups-list-path.ts";

export const AD_GROUP_RETURN_LABEL_DETAIL = "adGroupDetail";
export const AD_GROUP_RETURN_LABEL_LIST = "adGroupsList";

export type AdGroupNavigationState = {
  returnTo?: string;
  returnLabel?: string;
};

const AD_GROUPS_LIST_FALLBACK = AD_GROUPS_LIST_PATH;

const BLOCKED_RETURN_TO_SCHEMES = [
  "http:",
  "https:",
  "javascript:",
  "data:",
  "file:",
];

function containsBlockedReturnScheme(path: string): boolean {
  const lower = path.toLowerCase();
  if (lower.includes("//")) {
    return true;
  }

  return BLOCKED_RETURN_TO_SCHEMES.some((scheme) => lower.includes(scheme));
}

function containsParentDirectorySegment(path: string): boolean {
  return path
    .split("/")
    .filter((segment) => segment.length > 0)
    .some((segment) => segment === "..");
}

export function resolveSafeAdGroupReturnPath(returnTo: string | null | undefined): string {
  if (!returnTo?.trim()) {
    return AD_GROUPS_LIST_FALLBACK;
  }

  let decoded = returnTo.trim();
  try {
    decoded = decodeURIComponent(decoded);
  } catch {
    return AD_GROUPS_LIST_FALLBACK;
  }

  if (!decoded.startsWith("/")) {
    return AD_GROUPS_LIST_FALLBACK;
  }

  if (containsBlockedReturnScheme(decoded)) {
    return AD_GROUPS_LIST_FALLBACK;
  }

  if (decoded.includes("\\") || decoded.includes("\0")) {
    return AD_GROUPS_LIST_FALLBACK;
  }

  if (containsParentDirectorySegment(decoded)) {
    return AD_GROUPS_LIST_FALLBACK;
  }

  return decoded;
}

export function buildAdGroupDetailReturnState(groupId: string): AdGroupNavigationState {
  return {
    returnTo: buildAdGroupDetailPath(groupId),
    returnLabel: AD_GROUP_RETURN_LABEL_DETAIL,
  };
}

export function buildAdGroupsListReturnState(): AdGroupNavigationState {
  return {
    returnTo: AD_GROUPS_LIST_PATH,
    returnLabel: AD_GROUP_RETURN_LABEL_LIST,
  };
}

export function readAdGroupReturnToFromState(state: unknown): string | undefined {
  if (!state || typeof state !== "object") {
    return undefined;
  }

  const returnTo = (state as AdGroupNavigationState).returnTo;
  return typeof returnTo === "string" ? returnTo : undefined;
}

export function resolveAdGroupReturnPath(
  state: unknown,
  fallback: string = AD_GROUPS_LIST_FALLBACK,
): string {
  const returnTo = readAdGroupReturnToFromState(state);
  if (!returnTo?.trim()) {
    return fallback;
  }

  return resolveSafeAdGroupReturnPath(returnTo);
}
