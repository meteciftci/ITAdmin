import { AxiosHeaders, type InternalAxiosRequestConfig } from "axios";

export const CSRF_COOKIE_NAME = "itadmin.csrf_token";
export const CSRF_HEADER_NAME = "X-CSRF-TOKEN";

const UNSAFE_HTTP_METHODS = new Set(["POST", "PUT", "PATCH", "DELETE"]);

export function isUnsafeHttpMethod(method: string | undefined): boolean {
  return UNSAFE_HTTP_METHODS.has((method ?? "GET").toUpperCase());
}

/** Reads the CSRF double-submit cookie set by the backend (non-HttpOnly, Path=/). */
export function getCsrfTokenFromCookie(): string | null {
  if (typeof document === "undefined") {
    return null;
  }

  const escapedName = CSRF_COOKIE_NAME.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = document.cookie.match(new RegExp(`(?:^|;\\s*)${escapedName}=([^;]*)`));
  if (!match?.[1]) {
    return null;
  }

  try {
    return decodeURIComponent(match[1]);
  } catch {
    return match[1];
  }
}

export function applyCsrfHeader(config: InternalAxiosRequestConfig): void {
  if (!isUnsafeHttpMethod(config.method)) {
    return;
  }

  const token = getCsrfTokenFromCookie();
  if (!token) {
    return;
  }

  if (!config.headers) {
    config.headers = new AxiosHeaders();
  }

  if (config.headers instanceof AxiosHeaders) {
    config.headers.set(CSRF_HEADER_NAME, token);
  } else {
    (config.headers as Record<string, string>)[CSRF_HEADER_NAME] = token;
  }
}
