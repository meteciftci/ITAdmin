import { AxiosError } from "axios";

type ApiErrorData = {
  message?: string;
  title?: string;
} | string;

export function getApiErrorMessage(error: unknown, fallback: string): string {
  const axiosError = error as AxiosError<ApiErrorData>;
  const data = axiosError.response?.data;

  if (typeof data === "string" && data.trim()) {
    return data;
  }

  if (data && typeof data === "object") {
    if (typeof data.message === "string" && data.message.trim()) {
      return data.message;
    }

    if (typeof data.title === "string" && data.title.trim()) {
      return data.title;
    }
  }

  return fallback;
}

export type ApiErrorKind =
  | "network"
  | "serviceUnavailable"
  | "unauthorized"
  | "forbidden"
  | "server"
  | "validation"
  | "unknown";

export type ApiErrorCode =
  | "API_UNREACHABLE"
  | "SERVICE_UNAVAILABLE"
  | "UNAUTHORIZED"
  | "FORBIDDEN"
  | "SERVER_ERROR"
  | "VALIDATION_ERROR"
  | "UNKNOWN_ERROR";

export type ApiErrorInfo = {
  kind: ApiErrorKind;
  code: ApiErrorCode;
  titleKey: string;
  descriptionKey: string;
  status?: number;
  traceId?: string | null;
  originalMessage?: string | null;
};

type GetApiErrorInfoOptions = {
  fallbackTitle?: string;
  fallbackDescription?: string;
};

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function pickTraceIdFromData(data: unknown): string | null {
  if (!isPlainObject(data)) return null;
  const keys = [
    "traceId",
    "TraceId",
    "traceID",
    "trace_id",
    "correlationId",
    "CorrelationId",
  ] as const;
  for (const key of keys) {
    const v = data[key];
    if (typeof v === "string" && v.trim()) return v.trim();
  }
  return null;
}

function pickOriginalMessageFromData(data: unknown): string | null {
  if (!isPlainObject(data)) return null;
  const keys = ["message", "Message", "title", "detail", "error"] as const;
  for (const key of keys) {
    const v = data[key];
    if (typeof v === "string" && v.trim()) return v.trim();
  }
  return null;
}

function mapStatusToInfo(status: number): Omit<ApiErrorInfo, "traceId" | "originalMessage" | "status"> & {
  status: number;
} {
  if (status === 401) {
    return {
      kind: "unauthorized",
      code: "UNAUTHORIZED",
      titleKey: "errors:api.unauthorized.title",
      descriptionKey: "errors:api.unauthorized.description",
      status,
    };
  }
  if (status === 403) {
    return {
      kind: "forbidden",
      code: "FORBIDDEN",
      titleKey: "errors:api.forbidden.title",
      descriptionKey: "errors:api.forbidden.description",
      status,
    };
  }
  if (status === 400 || status === 422) {
    return {
      kind: "validation",
      code: "VALIDATION_ERROR",
      titleKey: "errors:api.validation.title",
      descriptionKey: "errors:api.validation.description",
      status,
    };
  }
  if (status === 502 || status === 503 || status === 504) {
    return {
      kind: "serviceUnavailable",
      code: "SERVICE_UNAVAILABLE",
      titleKey: "errors:api.serviceUnavailable.title",
      descriptionKey: "errors:api.serviceUnavailable.description",
      status,
    };
  }
  if (status >= 500) {
    return {
      kind: "server",
      code: "SERVER_ERROR",
      titleKey: "errors:api.server.title",
      descriptionKey: "errors:api.server.description",
      status,
    };
  }
  return {
    kind: "unknown",
    code: "UNKNOWN_ERROR",
    titleKey: "errors:api.unknown.title",
    descriptionKey: "errors:api.unknown.description",
    status,
  };
}

/**
 * Classifies an API/Axios error for user-facing error states.
 * Prefer {@link ApiErrorInfo.titleKey} / {@link ApiErrorInfo.descriptionKey} with i18n in UI.
 * `options` is reserved for callers; unknown-kind fallbacks are applied in {@link ApiErrorState}.
 */
export function getApiErrorInfo(
  error: unknown,
  _options?: GetApiErrorInfoOptions,
): ApiErrorInfo {
  void _options;

  if (!(error instanceof AxiosError)) {
    return {
      kind: "unknown",
      code: "UNKNOWN_ERROR",
      titleKey: "errors:api.unknown.title",
      descriptionKey: "errors:api.unknown.description",
      traceId: null,
      originalMessage: null,
    };
  }

  const response = error.response;
  const data = response?.data;

  if (!response) {
    return {
      kind: "network",
      code: "API_UNREACHABLE",
      titleKey: "errors:api.network.title",
      descriptionKey: "errors:api.network.description",
      traceId: null,
      originalMessage: pickOriginalMessageFromData(data) ?? null,
    };
  }

  const status = response.status;
  const base = mapStatusToInfo(status);
  const traceId = pickTraceIdFromData(data);
  const originalMessage = pickOriginalMessageFromData(data);

  return {
    ...base,
    status,
    traceId: traceId ?? null,
    originalMessage: originalMessage ?? null,
  };
}
