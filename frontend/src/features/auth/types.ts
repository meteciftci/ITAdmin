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

// Raw tokens are not part of the API response anymore: the backend delivers them only as
// HttpOnly cookies. The login/refresh JSON payload now only carries UI-relevant signals.
export type LoginResponse = {
  isSuccess: boolean;
  message: string;
  errorCode?: string | null;
};

export type RefreshTokenResponse = {
  isSuccess: boolean;
  message: string;
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
