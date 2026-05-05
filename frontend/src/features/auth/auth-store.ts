import { create } from "zustand";
import { persist } from "zustand/middleware";

import type { CurrentUser } from "@/features/auth/types";

type TokenPayload = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
};

type AuthState = {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAt: string | null;
  refreshTokenExpiresAt: string | null;
  user: CurrentUser | null;
  isAuthenticated: boolean;
  setTokens: (tokens: TokenPayload) => void;
  setUser: (user: CurrentUser | null) => void;
  clearAuth: () => void;
};

const initialState = {
  accessToken: null,
  refreshToken: null,
  accessTokenExpiresAt: null,
  refreshTokenExpiresAt: null,
  user: null,
  isAuthenticated: false,
};

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      ...initialState,
      setTokens: (tokens) =>
        set({
          ...tokens,
          isAuthenticated: Boolean(tokens.accessToken),
        }),
      setUser: (user) => set({ user }),
      clearAuth: () => set(initialState),
    }),
    {
      name: "sasportal-auth",
      partialize: (state) => ({
        accessToken: state.accessToken,
        refreshToken: state.refreshToken,
        accessTokenExpiresAt: state.accessTokenExpiresAt,
        refreshTokenExpiresAt: state.refreshTokenExpiresAt,
        user: state.user,
        isAuthenticated: state.isAuthenticated,
      }),
    },
  ),
);
