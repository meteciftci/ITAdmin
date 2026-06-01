import { unwrapJsonLikeString } from "@/lib/parse-json-like-value";

export type AdOperationErrorDiagnostic = {
  code?: string;
  operation?: string;
  step?: string;
  attribute?: string;
  normalizedReason?: string;
  message?: string;
  ldapResultCode?: number;
  ldapExceptionErrorCode?: number;
  partialUpdate?: boolean;
  rollbackStatus?: string;
  targetObjectGuid?: string;
  ldapDiagnosticMessage?: string;
};

export type ParsedAdOperationErrorMessage =
  | { kind: "structured"; diagnostic: AdOperationErrorDiagnostic }
  | { kind: "plainText"; text: string };

function readOptionalString(value: unknown): string | undefined {
  if (typeof value !== "string") {
    return undefined;
  }
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

function readOptionalNumber(value: unknown): number | undefined {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string" && value.trim().length > 0) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : undefined;
  }
  return undefined;
}

function readOptionalBoolean(value: unknown): boolean | undefined {
  if (typeof value === "boolean") {
    return value;
  }
  return undefined;
}

function mapDiagnosticPayload(payload: Record<string, unknown>): AdOperationErrorDiagnostic {
  return {
    code: readOptionalString(payload.code),
    operation: readOptionalString(payload.operation),
    step: readOptionalString(payload.step),
    attribute: readOptionalString(payload.attribute),
    normalizedReason: readOptionalString(payload.normalizedReason),
    message: readOptionalString(payload.message),
    ldapResultCode: readOptionalNumber(payload.ldapResultCode),
    ldapExceptionErrorCode: readOptionalNumber(payload.ldapExceptionErrorCode),
    partialUpdate: readOptionalBoolean(payload.partialUpdate),
    rollbackStatus: readOptionalString(payload.rollbackStatus),
    targetObjectGuid: readOptionalString(payload.targetObjectGuid),
    ldapDiagnosticMessage: readOptionalString(payload.ldapDiagnosticMessage),
  };
}

export function parseAdOperationErrorMessage(
  errorMessage: string | null | undefined,
): ParsedAdOperationErrorMessage | null {
  if (!errorMessage?.trim()) {
    return null;
  }

  const trimmed = errorMessage.trim();
  if (!trimmed.startsWith("{")) {
    return { kind: "plainText", text: trimmed };
  }

  try {
    const parsed: unknown = JSON.parse(trimmed);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return { kind: "plainText", text: trimmed };
    }

    return {
      kind: "structured",
      diagnostic: mapDiagnosticPayload(parsed as Record<string, unknown>),
    };
  } catch {
    return { kind: "plainText", text: trimmed };
  }
}

export function getAdOperationErrorSummary(
  parsed: ParsedAdOperationErrorMessage | null,
): string | null {
  if (!parsed) {
    return null;
  }

  if (parsed.kind === "plainText") {
    return parsed.text;
  }

  const { diagnostic } = parsed;
  return (
    diagnostic.message ??
    diagnostic.normalizedReason ??
    diagnostic.code ??
    null
  );
}

export function parseRequestSummaryChangeStatus(
  requestSummaryJson: string | null | undefined,
): string | null {
  if (!requestSummaryJson?.trim()) {
    return null;
  }

  const unwrapped = unwrapJsonLikeString(requestSummaryJson.trim());
  if (!unwrapped || typeof unwrapped !== "object" || Array.isArray(unwrapped)) {
    return null;
  }

  const changeStatus = (unwrapped as Record<string, unknown>).changeStatus;
  if (typeof changeStatus === "string" && changeStatus.trim().length > 0) {
    return changeStatus.trim();
  }

  return null;
}
