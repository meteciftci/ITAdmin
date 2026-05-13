import { apiClient } from "@/lib/api-client";
import type {
  AuthSessionOptions,
  CurrentUser,
  LoginRequest,
  LoginResponse,
  LogoutRequest,
  RefreshTokenRequest,
  RefreshTokenResponse,
} from "@/features/auth/types";

export const AUTH_SESSION_OPTIONS_DEFAULTS: AuthSessionOptions = {
  rememberMeEnabled: true,
  idleTimeoutMinutes: 30,
  idleWarningSeconds: 30,
  accessTokenMinutes: 30,
};

const toFiniteInt = (value: unknown): number | null => {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return null;
  }
  const rounded = Math.trunc(value);
  return rounded > 0 ? rounded : null;
};

const normalizeAuthSessionOptions = (raw: Partial<AuthSessionOptions> | null | undefined): AuthSessionOptions => {
  const idleTimeoutMinutes =
    toFiniteInt(raw?.idleTimeoutMinutes) ?? AUTH_SESSION_OPTIONS_DEFAULTS.idleTimeoutMinutes;
  const rawIdleWarningSeconds = toFiniteInt(raw?.idleWarningSeconds);
  const totalIdleSeconds = idleTimeoutMinutes * 60;
  const idleWarningSeconds =
    rawIdleWarningSeconds !== null && rawIdleWarningSeconds < totalIdleSeconds
      ? rawIdleWarningSeconds
      : Math.min(AUTH_SESSION_OPTIONS_DEFAULTS.idleWarningSeconds, Math.max(1, totalIdleSeconds - 1));

  return {
    rememberMeEnabled:
      typeof raw?.rememberMeEnabled === "boolean"
        ? raw.rememberMeEnabled
        : AUTH_SESSION_OPTIONS_DEFAULTS.rememberMeEnabled,
    idleTimeoutMinutes,
    idleWarningSeconds,
    accessTokenMinutes:
      toFiniteInt(raw?.accessTokenMinutes) ?? AUTH_SESSION_OPTIONS_DEFAULTS.accessTokenMinutes,
  };
};

export const getAuthSessionOptions = async (): Promise<AuthSessionOptions> => {
  const { data } = await apiClient.get<Partial<AuthSessionOptions>>("/auth/session-options");
  return normalizeAuthSessionOptions(data);
};

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
