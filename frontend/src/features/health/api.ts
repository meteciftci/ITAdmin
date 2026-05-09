import axios from "axios";

import { apiClient } from "@/lib/api-client";

import type { ReadinessResponse } from "./types";

const API_UNREACHABLE: ReadinessResponse = {
  status: "Unhealthy",
  apiAvailable: false,
  databaseAvailable: false,
  message: "API service is unreachable.",
};

const READINESS_CHECK_FAILED = (
  apiAvailable: boolean,
): ReadinessResponse => ({
  status: "Unhealthy",
  apiAvailable,
  databaseAvailable: false,
  message: "Service readiness check failed.",
});

export async function getReadinessStatus(): Promise<ReadinessResponse> {
  try {
    const { data } = await apiClient.get<ReadinessResponse>("/health/readiness");
    return data;
  } catch (error: unknown) {
    if (axios.isAxiosError(error)) {
      const status = error.response?.status;
      const payload = error.response?.data;

      if (
        status === 503 &&
        payload &&
        typeof payload === "object" &&
        "status" in payload &&
        typeof (payload as ReadinessResponse).apiAvailable === "boolean"
      ) {
        return payload as ReadinessResponse;
      }

      if (!error.response) {
        return API_UNREACHABLE;
      }

      return READINESS_CHECK_FAILED(true);
    }

    return READINESS_CHECK_FAILED(false);
  }
}
