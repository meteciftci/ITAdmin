import { AxiosError } from "axios";

type ApiErrorData = {
  message?: string;
  Message?: string;
  title?: string;
  Title?: string;
  detail?: string;
  Detail?: string;
  messageKey?: string;
  MessageKey?: string;
  titleKey?: string;
  TitleKey?: string;
  errors?: Record<string, string[]>;
  Errors?: Record<string, string[]>;
} | string;

export type ApiErrorMessageOptions = {
  genericValidationMessage?: string;
  fieldLabelResolver?: (fieldPath: string) => string | null;
};

const GENERIC_VALIDATION_TITLES = new Set([
  "one or more validation errors occurred.",
  "istek doğrulanamadı",
  "validation failed.",
]);

function isTechnicalValidationMessage(message: string): boolean {
  const normalized = message.toLowerCase();
  return (
    normalized.includes("could not be converted")
    || normalized.includes("json value")
    || normalized.includes("path:")
    || normalized.startsWith("the request field is required")
    || normalized.includes("request field is required")
  );
}

function pickValidationErrors(data: unknown): Record<string, string[]> | null {
  if (!isPlainObject(data)) {
    return null;
  }

  const errors = data.errors ?? data.Errors;
  if (!isPlainObject(errors)) {
    return null;
  }

  const result: Record<string, string[]> = {};
  for (const [key, value] of Object.entries(errors)) {
    if (!Array.isArray(value)) {
      continue;
    }

    const messages = value
      .filter((item): item is string => typeof item === "string" && item.trim().length > 0)
      .map((item) => item.trim());
    if (messages.length > 0) {
      result[key] = messages;
    }
  }

  return Object.keys(result).length > 0 ? result : null;
}

function normalizeValidationFieldPath(path: string): string {
  return path.replace(/^\$\./, "").replace(/^request\./, "").trim();
}

function formatValidationErrorMessage(
  errors: Record<string, string[]>,
  options?: ApiErrorMessageOptions,
): string | null {
  const genericMessage = options?.genericValidationMessage;
  const formatted = new Set<string>();

  for (const [rawPath, messages] of Object.entries(errors)) {
    const fieldPath = normalizeValidationFieldPath(rawPath);
    const fieldLabel = options?.fieldLabelResolver?.(fieldPath) ?? null;

    if (rawPath === "request" || fieldPath === "request") {
      continue;
    }

    for (const message of messages) {
      if (isTechnicalValidationMessage(message)) {
        if (fieldLabel && genericMessage) {
          formatted.add(`${fieldLabel}: ${genericMessage}`);
        } else if (genericMessage) {
          formatted.add(genericMessage);
        }
        continue;
      }

      if (fieldLabel) {
        formatted.add(`${fieldLabel}: ${message}`);
      } else {
        formatted.add(message);
      }
    }
  }

  if (formatted.size > 0) {
    return [...formatted].join("\n");
  }

  return genericMessage ?? null;
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function pickStringField(data: unknown, keys: readonly string[]): string | null {
  if (!isPlainObject(data)) return null;
  for (const key of keys) {
    const value = data[key];
    if (typeof value === "string" && value.trim()) {
      return value.trim();
    }
  }

  return null;
}

function pickMessageKeyFromData(data: unknown): string | null {
  return pickStringField(data, ["messageKey", "MessageKey"]);
}

function pickTitleKeyFromData(data: unknown): string | null {
  return pickStringField(data, ["titleKey", "TitleKey"]);
}

function pickOriginalMessageFromData(data: unknown): string | null {
  return pickStringField(data, ["message", "Message", "title", "Title", "detail", "Detail", "error"]);
}

export function getApiErrorMessage(
  error: unknown,
  fallback: string,
  options?: ApiErrorMessageOptions,
): string {
  const axiosError = error as AxiosError<ApiErrorData>;
  const data = axiosError.response?.data;

  if (typeof data === "string" && data.trim()) {
    return data;
  }

  if (data && typeof data === "object") {
    const validationErrors = pickValidationErrors(data);
    if (validationErrors) {
      const validationMessage = formatValidationErrorMessage(validationErrors, options);
      if (validationMessage) {
        return validationMessage;
      }
    }

    const message = pickOriginalMessageFromData(data);
    if (message) {
      const normalizedTitle = message.toLowerCase();
      if (GENERIC_VALIDATION_TITLES.has(normalizedTitle) && options?.genericValidationMessage) {
        return options.genericValidationMessage;
      }
      return message;
    }

    const messageKey = pickMessageKeyFromData(data);
    if (messageKey) {
      return messageKey;
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
  responseMessageKey?: string | null;
  responseTitleKey?: string | null;
};

type GetApiErrorInfoOptions = {
  fallbackTitle?: string;
  fallbackDescription?: string;
};

function pickTraceIdFromData(data: unknown): string | null {
  return pickStringField(data, [
    "traceId",
    "TraceId",
    "traceID",
    "trace_id",
    "correlationId",
    "CorrelationId",
  ]);
}

function mapStatusToInfo(status: number): Omit<ApiErrorInfo, "traceId" | "originalMessage" | "status" | "responseMessageKey" | "responseTitleKey"> & {
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
 * When the API returns `messageKey`/`titleKey`, they are exposed for optional i18n resolution.
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
      responseMessageKey: null,
      responseTitleKey: null,
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
      originalMessage: pickOriginalMessageFromData(data) ?? pickMessageKeyFromData(data) ?? null,
      responseMessageKey: pickMessageKeyFromData(data),
      responseTitleKey: pickTitleKeyFromData(data),
    };
  }

  const status = response.status;
  const base = mapStatusToInfo(status);
  const traceId = pickTraceIdFromData(data);
  const originalMessage =
    pickOriginalMessageFromData(data) ?? pickMessageKeyFromData(data) ?? null;

  return {
    ...base,
    status,
    traceId: traceId ?? null,
    originalMessage,
    responseMessageKey: pickMessageKeyFromData(data),
    responseTitleKey: pickTitleKeyFromData(data),
  };
}
