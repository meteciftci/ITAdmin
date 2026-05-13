import type { ApiErrorCode, ApiErrorInfo, ApiErrorKind } from "@/lib/api-error";
import { getApiErrorInfo } from "@/lib/api-error";

export type RouteErrorCode = ApiErrorCode | "NOT_FOUND";

export type RouteErrorState = {
  kind?: ApiErrorKind;
  code: RouteErrorCode;
  status?: number;
  traceId?: string | null;
  titleKey?: string;
  descriptionKey?: string;
  fromPath?: string;
  retryPath?: string;
  backPath?: string;
  sourceLabel?: string;
  originalMessage?: string | null;
};

const ERROR_CODE_TO_SLUG: Record<RouteErrorCode, string> = {
  API_UNREACHABLE: "api-unreachable",
  SERVICE_UNAVAILABLE: "service-unavailable",
  UNAUTHORIZED: "unauthorized",
  FORBIDDEN: "forbidden",
  SERVER_ERROR: "server-error",
  VALIDATION_ERROR: "validation-error",
  UNKNOWN_ERROR: "unknown-error",
  NOT_FOUND: "not-found",
};

/** URL segment for `/error/:code` derived from {@link ApiErrorInfo}. */
export function getErrorRouteCode(info: ApiErrorInfo): string {
  return ERROR_CODE_TO_SLUG[info.code];
}

export function getErrorRoutePath(code: RouteErrorCode): string {
  return `/error/${ERROR_CODE_TO_SLUG[code]}`;
}

type SlugDefaults = {
  code: RouteErrorCode;
  kind?: ApiErrorKind;
  titleKey: string;
  descriptionKey: string;
};

export const ERROR_ROUTE_SLUG_DEFAULTS: Record<string, SlugDefaults> = {
  "api-unreachable": {
    code: "API_UNREACHABLE",
    kind: "network",
    titleKey: "errors:api.network.title",
    descriptionKey: "errors:api.network.description",
  },
  "service-unavailable": {
    code: "SERVICE_UNAVAILABLE",
    kind: "serviceUnavailable",
    titleKey: "errors:api.serviceUnavailable.title",
    descriptionKey: "errors:api.serviceUnavailable.description",
  },
  unauthorized: {
    code: "UNAUTHORIZED",
    kind: "unauthorized",
    titleKey: "errors:api.unauthorized.title",
    descriptionKey: "errors:api.unauthorized.description",
  },
  forbidden: {
    code: "FORBIDDEN",
    kind: "forbidden",
    titleKey: "errors:api.forbidden.title",
    descriptionKey: "errors:api.forbidden.description",
  },
  "server-error": {
    code: "SERVER_ERROR",
    kind: "server",
    titleKey: "errors:api.server.title",
    descriptionKey: "errors:api.server.description",
  },
  "validation-error": {
    code: "VALIDATION_ERROR",
    kind: "validation",
    titleKey: "errors:api.validation.title",
    descriptionKey: "errors:api.validation.description",
  },
  "unknown-error": {
    code: "UNKNOWN_ERROR",
    kind: "unknown",
    titleKey: "errors:api.unknown.title",
    descriptionKey: "errors:api.unknown.description",
  },
  "not-found": {
    code: "NOT_FOUND",
    titleKey: "errors:route.notFound.title",
    descriptionKey: "errors:route.notFound.description",
  },
};

export function resolveErrorRouteFromSlug(slug: string | undefined): SlugDefaults | null {
  if (!slug) return null;
  return ERROR_ROUTE_SLUG_DEFAULTS[slug] ?? null;
}

export function isRouteErrorState(value: unknown): value is RouteErrorState {
  if (!value || typeof value !== "object") return false;
  const o = value as Record<string, unknown>;
  if (typeof o.code !== "string") return false;
  const validCodes = new Set<string>([
    "API_UNREACHABLE",
    "SERVICE_UNAVAILABLE",
    "UNAUTHORIZED",
    "FORBIDDEN",
    "SERVER_ERROR",
    "VALIDATION_ERROR",
    "UNKNOWN_ERROR",
    "NOT_FOUND",
  ]);
  return validCodes.has(o.code);
}

export function createApiErrorRouteState(
  error: unknown,
  options?: { fromPath?: string; retryPath?: string; sourceLabel?: string },
): RouteErrorState {
  const info = getApiErrorInfo(error);
  return {
    kind: info.kind,
    code: info.code,
    status: info.status,
    traceId: info.traceId ?? null,
    titleKey: info.titleKey,
    descriptionKey: info.descriptionKey,
    fromPath: options?.fromPath,
    retryPath: options?.retryPath,
    sourceLabel: options?.sourceLabel,
    originalMessage: info.originalMessage ?? null,
  };
}
