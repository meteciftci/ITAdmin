import { buildAdUserDetailPath } from "./ad-user-detail-path.ts";
import { AD_USERS_LIST_PATH } from "./ad-users-list-path.ts";

export const AD_USER_RETURN_LABEL_DETAIL = "adUserDetail";

export type AdUserNavigationState = {
  returnTo?: string;
  returnLabel?: string;
};

const AD_USERS_LIST_FALLBACK = AD_USERS_LIST_PATH;

const BLOCKED_RETURN_TO_PREFIXES = [
  "http:",
  "https:",
  "javascript:",
  "data:",
  "file:",
  "//",
];

export function resolveSafeReturnPath(returnTo: string | null | undefined): string {
  if (!returnTo?.trim()) {
    return AD_USERS_LIST_FALLBACK;
  }

  let decoded = returnTo.trim();
  try {
    decoded = decodeURIComponent(decoded);
  } catch {
    return AD_USERS_LIST_FALLBACK;
  }

  if (!decoded.startsWith("/")) {
    return AD_USERS_LIST_FALLBACK;
  }

  const lower = decoded.toLowerCase();
  if (BLOCKED_RETURN_TO_PREFIXES.some((prefix) => lower.startsWith(prefix))) {
    return AD_USERS_LIST_FALLBACK;
  }

  if (decoded.includes("\\") || decoded.includes("\0")) {
    return AD_USERS_LIST_FALLBACK;
  }

  return decoded;
}

export function buildReturnToQueryParam(path: string): string {
  return encodeURIComponent(path.startsWith("/") ? path : `/${path}`);
}

export function appendReturnTo(path: string, returnTo: string): string {
  const separator = path.includes("?") ? "&" : "?";
  return `${path}${separator}returnTo=${buildReturnToQueryParam(returnTo)}`;
}

export function buildAdUserDetailReturnState(userId: string): AdUserNavigationState {
  return {
    returnTo: buildAdUserDetailPath(userId),
    returnLabel: AD_USER_RETURN_LABEL_DETAIL,
  };
}

export function readAdUserReturnToFromState(state: unknown): string | undefined {
  if (!state || typeof state !== "object") {
    return undefined;
  }

  const returnTo = (state as AdUserNavigationState).returnTo;
  return typeof returnTo === "string" ? returnTo : undefined;
}

export function resolveAdUserReturnPath(
  state: unknown,
  fallback: string = AD_USERS_LIST_FALLBACK,
): string {
  const returnTo = readAdUserReturnToFromState(state);
  if (!returnTo?.trim()) {
    return fallback;
  }

  return resolveSafeReturnPath(returnTo);
}

/** @deprecated Prefer location.state; kept for legacy query-based returnTo links. */
export function readAdUserReturnToFromSearchParams(
  searchParams: URLSearchParams,
): string | undefined {
  const returnTo = searchParams.get("returnTo");
  return returnTo?.trim() ? returnTo : undefined;
}

export function resolveAdUserReturnPathFromLocation(
  state: unknown,
  searchParams?: URLSearchParams,
  fallback: string = AD_USERS_LIST_FALLBACK,
): string {
  const fromState = readAdUserReturnToFromState(state);
  if (fromState?.trim()) {
    return resolveSafeReturnPath(fromState);
  }

  const fromQuery = searchParams
    ? readAdUserReturnToFromSearchParams(searchParams)
    : undefined;
  if (fromQuery) {
    return resolveSafeReturnPath(fromQuery);
  }

  return fallback;
}
