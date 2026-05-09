import axios from "axios";

import { apiClient } from "@/lib/api-client";

import type { ReadinessResponse } from "./types";

const API_UNREACHABLE_MESSAGE = "API service is unreachable.";
const READINESS_CHECK_FAILED_MESSAGE = "Service readiness check failed.";

export function parseReadinessPayload(data: unknown): ReadinessResponse | null {
  if (!data || typeof data !== "object") {
    return null;
  }
  const o = data as Record<string, unknown>;
  if (
    typeof o.status !== "string" ||
    typeof o.apiAvailable !== "boolean" ||
    typeof o.databaseAvailable !== "boolean" ||
    typeof o.message !== "string"
  ) {
    return null;
  }
  const ldapAvailable =
    typeof o.ldapAvailable === "boolean" ? o.ldapAvailable : false;
  const traceId = o.traceId;
  return {
    status: o.status,
    apiAvailable: o.apiAvailable,
    databaseAvailable: o.databaseAvailable,
    ldapAvailable,
    message: o.message,
    traceId:
      typeof traceId === "string" || traceId === null
        ? (traceId as string | null)
        : undefined,
    checkedAt: typeof o.checkedAt === "string" ? o.checkedAt : undefined,
  };
}

export function isValidReadinessResponse(data: unknown): data is ReadinessResponse {
  return parseReadinessPayload(data) !== null;
}

export function createApiUnreachableResponse(): ReadinessResponse {
  return {
    status: "Unhealthy",
    apiAvailable: false,
    databaseAvailable: false,
    ldapAvailable: false,
    message: API_UNREACHABLE_MESSAGE,
    checkedAt: new Date().toISOString(),
  };
}

export function createReadinessCheckFailedResponse(
  apiAvailable: boolean,
): ReadinessResponse {
  return {
    status: "Unhealthy",
    apiAvailable,
    databaseAvailable: false,
    ldapAvailable: false,
    message: READINESS_CHECK_FAILED_MESSAGE,
    checkedAt: new Date().toISOString(),
  };
}

/** Maps /auth/me (or similar) Axios failures to a readiness-shaped payload. */
export function getSyntheticReadinessForAxiosError(
  error: unknown,
): ReadinessResponse {
  if (!axios.isAxiosError(error)) {
    return createReadinessCheckFailedResponse(false);
  }

  const status = error.response?.status;
  const payload = error.response?.data;

  if (!error.response) {
    return createApiUnreachableResponse();
  }

  if (status === 502 || status === 503 || status === 504) {
    const parsed = parseReadinessPayload(payload);
    if (parsed) {
      return parsed;
    }
    return createApiUnreachableResponse();
  }

  if (status === 500) {
    const parsed = parseReadinessPayload(payload);
    if (parsed) {
      return parsed;
    }
    return createReadinessCheckFailedResponse(true);
  }

  return createReadinessCheckFailedResponse(true);
}

export async function getReadinessStatus(): Promise<ReadinessResponse> {
  try {
    const { data } = await apiClient.get<ReadinessResponse>("/health/readiness");
    const parsed = parseReadinessPayload(data);
    if (parsed) {
      return parsed;
    }
    return createReadinessCheckFailedResponse(true);
  } catch (error: unknown) {
    if (!axios.isAxiosError(error)) {
      return createReadinessCheckFailedResponse(false);
    }

    const status = error.response?.status;
    const payload = error.response?.data;

    if (!error.response) {
      return createApiUnreachableResponse();
    }

    if (status === 502 || status === 503 || status === 504) {
      const parsed = parseReadinessPayload(payload);
      if (parsed) {
        return parsed;
      }
      return createApiUnreachableResponse();
    }

    if (status === 500) {
      const parsed = parseReadinessPayload(payload);
      if (parsed) {
        return parsed;
      }
      return createReadinessCheckFailedResponse(true);
    }

    return createReadinessCheckFailedResponse(true);
  }
}
