import { create } from "zustand";

import type { CurrentUser } from "@/features/auth/types";

type TokenPayload = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
};

export type AuthStorageMode = "session" | "persistent";

type StoredAuthSnapshot = {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAt: string | null;
  refreshTokenExpiresAt: string | null;
  user: CurrentUser | null;
  isAuthenticated: boolean;
  rememberMe: boolean;
  storageMode: AuthStorageMode;
  updatedAt: number;
};

/**
 * Legacy Zustand persist key: tokens lived in localStorage only, so closing the browser
 * still restored the session. For security we intentionally remove this key on load so
 * users re-authenticate once under the session vs persistent storage split.
 */
const LEGACY_AUTH_STORAGE_KEY = "sasportal-auth";

export const AUTH_SESSION_STORAGE_KEY = "sasportal-auth-session";
export const AUTH_PERSISTENT_STORAGE_KEY = "sasportal-auth-persistent";

type AuthState = {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAt: string | null;
  refreshTokenExpiresAt: string | null;
  user: CurrentUser | null;
  isAuthenticated: boolean;
  rememberMe: boolean;
  storageMode: AuthStorageMode | null;
  setTokens: (tokens: TokenPayload, rememberMe: boolean) => void;
  setUser: (user: CurrentUser | null) => void;
  updateUser: (patch: Partial<CurrentUser>) => void;
  clearAuth: () => void;
  hydrateAuthFromStorage: () => void;
};

const emptyTokens = {
  accessToken: null,
  refreshToken: null,
  accessTokenExpiresAt: null,
  refreshTokenExpiresAt: null,
};

function clearLegacyKeys(): void {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.removeItem(LEGACY_AUTH_STORAGE_KEY);
    window.sessionStorage.removeItem(LEGACY_AUTH_STORAGE_KEY);
  } catch {
    // ignore storage access errors
  }
}

function removeBothAuthKeys(): void {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.sessionStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
    window.localStorage.removeItem(AUTH_PERSISTENT_STORAGE_KEY);
  } catch {
    // ignore
  }
}

function parseSnapshot(raw: string): StoredAuthSnapshot | null {
  try {
    const parsed = JSON.parse(raw) as Partial<StoredAuthSnapshot>;
    if (
      !parsed ||
      typeof parsed !== "object" ||
      (parsed.storageMode !== "session" && parsed.storageMode !== "persistent")
    ) {
      return null;
    }

    return {
      accessToken: typeof parsed.accessToken === "string" ? parsed.accessToken : null,
      refreshToken: typeof parsed.refreshToken === "string" ? parsed.refreshToken : null,
      accessTokenExpiresAt:
        typeof parsed.accessTokenExpiresAt === "string" ? parsed.accessTokenExpiresAt : null,
      refreshTokenExpiresAt:
        typeof parsed.refreshTokenExpiresAt === "string" ? parsed.refreshTokenExpiresAt : null,
      user: (parsed.user ?? null) as CurrentUser | null,
      isAuthenticated: Boolean(parsed.accessToken),
      rememberMe: Boolean(parsed.rememberMe),
      storageMode: parsed.storageMode,
      updatedAt: typeof parsed.updatedAt === "number" ? parsed.updatedAt : 0,
    };
  } catch {
    return null;
  }
}

function readBootstrapFromStorage(): Pick<
  AuthState,
  | "accessToken"
  | "refreshToken"
  | "accessTokenExpiresAt"
  | "refreshTokenExpiresAt"
  | "user"
  | "isAuthenticated"
  | "rememberMe"
  | "storageMode"
> {
  const base = {
    ...emptyTokens,
    user: null,
    isAuthenticated: false,
    rememberMe: false,
    storageMode: null as AuthStorageMode | null,
  };

  if (typeof window === "undefined") {
    return base;
  }

  clearLegacyKeys();

  try {
    const sessionRaw = window.sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY);
    if (sessionRaw) {
      const parsed = parseSnapshot(sessionRaw);
      if (parsed?.accessToken && parsed.storageMode === "session") {
        return {
          accessToken: parsed.accessToken,
          refreshToken: parsed.refreshToken,
          accessTokenExpiresAt: parsed.accessTokenExpiresAt,
          refreshTokenExpiresAt: parsed.refreshTokenExpiresAt,
          user: parsed.user,
          isAuthenticated: parsed.isAuthenticated,
          rememberMe: parsed.rememberMe,
          storageMode: "session",
        };
      }
    }

    const persistentRaw = window.localStorage.getItem(AUTH_PERSISTENT_STORAGE_KEY);
    if (persistentRaw) {
      const parsed = parseSnapshot(persistentRaw);
      if (parsed?.accessToken && parsed.storageMode === "persistent") {
        return {
          accessToken: parsed.accessToken,
          refreshToken: parsed.refreshToken,
          accessTokenExpiresAt: parsed.accessTokenExpiresAt,
          refreshTokenExpiresAt: parsed.refreshTokenExpiresAt,
          user: parsed.user,
          isAuthenticated: parsed.isAuthenticated,
          rememberMe: parsed.rememberMe,
          storageMode: "persistent",
        };
      }
    }
  } catch {
    return base;
  }

  return base;
}

function toSnapshot(state: {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAt: string | null;
  refreshTokenExpiresAt: string | null;
  user: CurrentUser | null;
  isAuthenticated: boolean;
  rememberMe: boolean;
  storageMode: AuthStorageMode;
}): StoredAuthSnapshot {
  return {
    accessToken: state.accessToken,
    refreshToken: state.refreshToken,
    accessTokenExpiresAt: state.accessTokenExpiresAt,
    refreshTokenExpiresAt: state.refreshTokenExpiresAt,
    user: state.user,
    isAuthenticated: state.isAuthenticated,
    rememberMe: state.rememberMe,
    storageMode: state.storageMode,
    updatedAt: Date.now(),
  };
}

function persistAuthSnapshot(state: {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAt: string | null;
  refreshTokenExpiresAt: string | null;
  user: CurrentUser | null;
  isAuthenticated: boolean;
  rememberMe: boolean;
  storageMode: AuthStorageMode;
}): void {
  if (typeof window === "undefined") {
    return;
  }

  const snapshot = toSnapshot(state);
  const targetStorage = state.storageMode === "persistent" ? window.localStorage : window.sessionStorage;
  const targetKey =
    state.storageMode === "persistent" ? AUTH_PERSISTENT_STORAGE_KEY : AUTH_SESSION_STORAGE_KEY;
  const otherStorage = state.storageMode === "persistent" ? window.sessionStorage : window.localStorage;
  const otherKey =
    state.storageMode === "persistent" ? AUTH_SESSION_STORAGE_KEY : AUTH_PERSISTENT_STORAGE_KEY;

  try {
    otherStorage.removeItem(otherKey);
    targetStorage.setItem(targetKey, JSON.stringify(snapshot));
  } catch {
    // ignore quota / private mode
  }
}

const boot = readBootstrapFromStorage();

export const useAuthStore = create<AuthState>((set) => ({
  ...boot,
  setTokens: (tokens, rememberMe) =>
    set((state) => {
      const storageMode: AuthStorageMode = rememberMe ? "persistent" : "session";
      const next = {
        ...state,
        ...tokens,
        rememberMe,
        storageMode,
        isAuthenticated: Boolean(tokens.accessToken),
      };
      persistAuthSnapshot(next);
      return next;
    }),
  setUser: (user) =>
    set((state) => {
      const next = { ...state, user };
      if (state.storageMode && state.accessToken) {
        persistAuthSnapshot({
          ...next,
          rememberMe: state.rememberMe,
          storageMode: state.storageMode,
        });
      }

      return next;
    }),
  updateUser: (patch) =>
    set((state) => {
      const nextUser = state.user ? { ...state.user, ...patch } : state.user;
      const next = { ...state, user: nextUser };
      if (state.storageMode && state.accessToken) {
        persistAuthSnapshot({
          ...next,
          rememberMe: state.rememberMe,
          storageMode: state.storageMode,
        });
      }

      return next;
    }),
  clearAuth: () => {
    removeBothAuthKeys();
    clearLegacyKeys();
    set({
      ...emptyTokens,
      user: null,
      isAuthenticated: false,
      rememberMe: false,
      storageMode: null,
    });
  },
  hydrateAuthFromStorage: () => {
    set(() => ({
      ...readBootstrapFromStorage(),
    }));
  },
}));
