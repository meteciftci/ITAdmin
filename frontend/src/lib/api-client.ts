import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";

import { useAuthStore } from "@/features/auth/auth-store";
import type { RefreshTokenResponse } from "@/features/auth/types";

type RetryableRequestConfig = InternalAxiosRequestConfig & { _retry?: boolean };

const authPaths = ["/auth/login", "/auth/refresh", "/auth/logout"];

export const apiClient = axios.create({
  baseURL: "/api",
});

const refreshClient = axios.create({
  baseURL: "/api",
});

apiClient.interceptors.request.use((config) => {
  const accessToken = useAuthStore.getState().accessToken;
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }

  return config;
});

let refreshPromise: Promise<RefreshTokenResponse | null> | null = null;

const attemptRefresh = async (): Promise<RefreshTokenResponse | null> => {
  const store = useAuthStore.getState();
  if (!store.refreshToken) {
    return null;
  }

  if (!refreshPromise) {
    refreshPromise = refreshClient
      .post<RefreshTokenResponse>("/auth/refresh", {
        refreshToken: store.refreshToken,
      })
      .then((response) => {
        if (!response.data.isSuccess) {
          return null;
        }

        useAuthStore.getState().setTokens({
          accessToken: response.data.accessToken,
          refreshToken: response.data.refreshToken,
          accessTokenExpiresAt: response.data.accessTokenExpiresAt,
          refreshTokenExpiresAt: response.data.refreshTokenExpiresAt,
        });

        return response.data;
      })
      .catch(() => null)
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
    const isAuthRequest = authPaths.some((path) => requestUrl.includes(path));

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
