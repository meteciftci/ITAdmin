import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";

import { isFutureExpiry, useAuthStore } from "@/features/auth/auth-store";
import type { RefreshTokenResponse } from "@/features/auth/types";

type RetryableRequestConfig = InternalAxiosRequestConfig & { _retry?: boolean };

const authPaths = ["/auth/login", "/auth/refresh", "/auth/logout"];

const urlLooksLikeAuthPath = (requestUrl: string | undefined): boolean => {
  if (!requestUrl) {
    return false;
  }

  return authPaths.some((path) => {
    const withoutLeadingSlash = path.replace(/^\//, "");
    return requestUrl.includes(path) || requestUrl.endsWith(withoutLeadingSlash);
  });
};

export const apiClient = axios.create({
  baseURL: "/api",
  withCredentials: true,
});

const refreshClient = axios.create({
  baseURL: "/api",
  withCredentials: true,
});

const UNSAFE_URL_SCHEME_REGEX = /^\s*(data|javascript|file):/i;

const normalizeOrigin = (value: string): string => value.replace(/\/+$/, "");

export const getApiOrigin = (): string => {
  const envOrigin = import.meta.env.VITE_API_ORIGIN?.trim();
  if (envOrigin) {
    return normalizeOrigin(envOrigin);
  }

  return normalizeOrigin(window.location.origin);
};

export const resolveApiAssetUrl = (pathOrUrl?: string | null): string | null => {
  const value = pathOrUrl?.trim();
  if (!value) {
    return null;
  }

  if (UNSAFE_URL_SCHEME_REGEX.test(value)) {
    return null;
  }

  if (/^https?:\/\//i.test(value)) {
    return value;
  }

  if (value.startsWith("/")) {
    return `${getApiOrigin()}${value}`;
  }

  return null;
};

apiClient.interceptors.request.use((config) => {
  const accessToken = useAuthStore.getState().accessToken;
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }

  return config;
});

let refreshPromise: Promise<RefreshTokenResponse | null> | null = null;

const attemptRefresh = async (): Promise<RefreshTokenResponse | null> => {
  if (!refreshPromise) {
    refreshPromise = refreshClient
      .post<RefreshTokenResponse>("/auth/refresh", undefined)
      .then((response) => {
        if (!response.data.isSuccess) {
          return null;
        }

        const { accessToken, accessTokenExpiresAt } = response.data;
        if (!accessToken?.trim() || !isFutureExpiry(accessTokenExpiresAt)) {
          return null;
        }

        useAuthStore.getState().setTokens(
          {
            accessToken,
            accessTokenExpiresAt,
          },
          useAuthStore.getState().rememberMe,
        );

        return response.data;
      })
      .catch((error: unknown) => {
        if (axios.isAxiosError(error) && error.response?.status === 401) {
          useAuthStore.getState().clearAuth();
        }

        return null;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
};

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as RetryableRequestConfig | undefined;
    const requestUrl = originalRequest?.url ?? "";
    const isAuthRequest = urlLooksLikeAuthPath(requestUrl);

    if (
      error.response?.status !== 401 ||
      !originalRequest ||
      originalRequest._retry ||
      isAuthRequest
    ) {
      return Promise.reject(error);
    }

    originalRequest._retry = true;
    const refreshed = await attemptRefresh();

    if (!refreshed) {
      useAuthStore.getState().clearAuth();
      if (window.location.pathname !== "/login") {
        window.location.assign("/login");
      }

      return Promise.reject(error);
    }

    originalRequest.headers.Authorization = `Bearer ${refreshed.accessToken}`;
    return apiClient(originalRequest);
  },
);
