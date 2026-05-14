export type LoginRequest = {
  userName: string;
  password: string;
  rememberMe: boolean;
};

export type AuthSessionOptions = {
  rememberMeEnabled: boolean;
  idleTimeoutMinutes: number;
  idleWarningSeconds: number;
  accessTokenMinutes: number;
};

export type LoginResponse = {
  isSuccess: boolean;
  message: string;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  errorCode?: string | null;
};

export type RefreshTokenResponse = {
  isSuccess: boolean;
  message: string;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  errorCode?: string | null;
};

export type CurrentUser = {
  userId: string;
  userName: string;
  displayName: string;
  email: string | null;
  roles: string[];
  permissions: string[];
  isSuperAdmin: boolean;
  preferredLanguage: "tr" | "en" | string;
};
