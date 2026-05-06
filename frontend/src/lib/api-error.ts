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
