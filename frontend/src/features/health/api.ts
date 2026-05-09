import axios from "axios";

import { apiClient } from "@/lib/api-client";

import type { ReadinessResponse } from "./types";

const API_UNREACHABLE_MESSAGE = "API service is unreachable.";
const READINESS_CHECK_FAILED_MESSAGE = "Service readiness check failed.";

export function createApiUnreachableResponse(): ReadinessResponse {
  return {
    status: "Unhealthy",
    apiAvailable: false,
    databaseAvailable: false,
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
    message: READINESS_CHECK_FAILED_MESSAGE,
    checkedAt: new Date().toISOString(),
  };
}

export function isValidReadinessResponse(
  data: unknown,
): data is ReadinessResponse {
  if (!data || typeof data !== "object") {
    return false;
  }
  const o = data as Record<string, unknown>;
  return (
    typeof o.status === "string" &&
    typeof o.apiAvailable === "boolean" &&
    typeof o.databaseAvailable === "boolean" &&
    typeof o.message === "string"
  );
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
    if (isValidReadinessResponse(payload)) {
      return payload;
    }
    return createApiUnreachableResponse();
  }

  if (status === 500) {
    if (isValidReadinessResponse(payload)) {
      return payload;
    }
    return createReadinessCheckFailedResponse(true);
  }

  return createReadinessCheckFailedResponse(true);
}

export async function getReadinessStatus(): Promise<ReadinessResponse> {
  try {
    const { data } = await apiClient.get<ReadinessResponse>("/health/readiness");
    if (isValidReadinessResponse(data)) {
      return data;
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
      if (isValidReadinessResponse(payload)) {
        return payload;
      }
      return createApiUnreachableResponse();
    }

    if (status === 500) {
      if (isValidReadinessResponse(payload)) {
        return payload;
      }
      return createReadinessCheckFailedResponse(true);
    }

    return createReadinessCheckFailedResponse(true);
  }
}
