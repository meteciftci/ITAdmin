const AD_USERS_LIST_FALLBACK = "/ad-management/users";

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
