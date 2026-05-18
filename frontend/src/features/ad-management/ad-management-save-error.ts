import { AxiosError } from "axios";

import type { AdManagementValidationResult } from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";

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
    typeof candidate.message !== "string" ||
    typeof candidate.checkedAt !== "string" ||
    !Array.isArray(candidate.details)
  ) {
    return null;
  }
  return {
    isValid: candidate.isValid,
    message: candidate.message,
    checkedAt: candidate.checkedAt,
    details: candidate.details
      .map((item) => {
        if (!item || typeof item !== "object") return null;
        const detail = item as Record<string, unknown>;
        if (
          typeof detail.key !== "string" ||
          typeof detail.status !== "string"
        ) {
          return null;
        }
        return {
          key: detail.key,
          status: detail.status,
          message:
            typeof detail.message === "string" ? detail.message : null,
        };
      })
      .filter((d): d is AdManagementValidationResult["details"][number] => d !== null),
  };
}

export function getAdManagementSaveErrorMessage(
  error: unknown,
  saveFailedFallback: string,
): string {
  const apiMessage = getApiErrorMessage(error, "");
  if (apiMessage.trim().length > 0) {
    return apiMessage;
  }

  const validation = extractValidationFromError(error);
  if (validation?.message.trim()) {
    return validation.message;
  }

  return saveFailedFallback;
}
