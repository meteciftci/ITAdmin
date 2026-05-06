import { apiClient } from "@/lib/api-client";
import type {
  CurrentUser,
  LoginRequest,
  LoginResponse,
  LogoutRequest,
  RefreshTokenRequest,
  RefreshTokenResponse,
} from "@/features/auth/types";

export const login = async (request: LoginRequest): Promise<LoginResponse> => {
  const { data } = await apiClient.post<LoginResponse>("/auth/login", request);
  return data;
};

export const refreshToken = async (
  refreshTokenValue: string,
): Promise<RefreshTokenResponse> => {
  const payload: RefreshTokenRequest = { refreshToken: refreshTokenValue };
  const { data } = await apiClient.post<RefreshTokenResponse>(
    "/auth/refresh",
    payload,
  );

  return data;
};

export const logout = async (refreshTokenValue: string): Promise<void> => {
  const payload: LogoutRequest = { refreshToken: refreshTokenValue };
  await apiClient.post("/auth/logout", payload);
};

export const getCurrentUser = async (): Promise<CurrentUser> => {
  const { data } = await apiClient.get<CurrentUser>("/auth/me");
  return data;
};

export type UpdateCurrentUserPreferencesRequest = {
  preferredLanguage: string;
};

export const updateCurrentUserPreferences = async (
  request: UpdateCurrentUserPreferencesRequest,
): Promise<CurrentUser> => {
  const { data } = await apiClient.patch<CurrentUser>("/auth/me/preferences", request);
  return data;
};
