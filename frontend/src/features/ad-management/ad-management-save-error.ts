import { AxiosError } from "axios";

import type { TFunction } from "i18next";

import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";
import type {
  AdManagementApiMessageParams,
  AdManagementValidationResult,
} from "@/features/ad-management/types";

function parseMessageParams(
  value: unknown,
): AdManagementApiMessageParams | null {
  if (value === null || value === undefined) {
    return null;
  }

  if (typeof value !== "object" || Array.isArray(value)) {
    return null;
  }

  return value as AdManagementApiMessageParams;
}

export function extractValidationFromError(
  error: unknown,
): AdManagementValidationResult | null {
  if (!(error instanceof AxiosError)) {
    return null;
  }
  const data = error.response?.data;
  if (!data || typeof data !== "object") {
    return null;
  }
  const raw = (data as { validation?: unknown }).validation;
  if (!raw || typeof raw !== "object") {
    return null;
  }
  const candidate = raw as Partial<AdManagementValidationResult> & {
    details?: unknown;
  };
  if (
    typeof candidate.isValid !== "boolean" ||
    typeof candidate.messageKey !== "string" ||
    typeof candidate.checkedAt !== "string" ||
    !Array.isArray(candidate.details)
  ) {
    return null;
  }
  return {
    isValid: candidate.isValid,
    messageKey: candidate.messageKey,
    messageParams: parseMessageParams(candidate.messageParams),
    checkedAt: candidate.checkedAt,
    details: candidate.details
      .map((item): AdManagementValidationResult["details"][number] | null => {
        if (!item || typeof item !== "object") return null;
        const detail = item as Record<string, unknown>;
        if (
          typeof detail.key !== "string" ||
          typeof detail.status !== "string" ||
          typeof detail.messageKey !== "string"
        ) {
          return null;
        }
        return {
          key: detail.key,
          status: detail.status,
          messageKey: detail.messageKey,
          messageParams: parseMessageParams(detail.messageParams),
        };
      })
      .filter((d): d is AdManagementValidationResult["details"][number] => d !== null),
  };
}

export function getAdManagementSaveErrorMessage(
  error: unknown,
  t: TFunction,
  fallbackKey: string,
): string {
  const validation = extractValidationFromError(error);
  if (validation) {
    return resolveAdManagementApiMessage(t, validation, fallbackKey);
  }

  return getAdManagementApiErrorMessage(error, t, fallbackKey);
}
