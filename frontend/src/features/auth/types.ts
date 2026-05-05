export type LoginRequest = {
  userName: string;
  password: string;
};

export type LoginResponse = {
  isSuccess: boolean;
  message: string;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
};

export type RefreshTokenRequest = {
  refreshToken: string;
};

export type RefreshTokenResponse = {
  isSuccess: boolean;
  message: string;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
};

export type LogoutRequest = {
  refreshToken: string;
};

export type CurrentUser = {
  userId: string;
  userName: string;
  displayName: string;
  email: string;
  roles: string[];
  permissions: string[];
  isSuperAdmin: boolean;
};
