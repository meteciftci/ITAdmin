import { create } from "zustand";

import type { CurrentUser } from "@/features/auth/types";

export type AuthStorageMode = "session" | "persistent";

type StoredAuthPreferences = {
  rememberMe: boolean;
  storageMode: AuthStorageMode;
  updatedAt: number;
};

const LEGACY_AUTH_STORAGE_KEY = "sasportal-auth";

const AUTH_SESSION_STORAGE_KEY = "sasportal-auth-session";
const AUTH_PERSISTENT_STORAGE_KEY = "sasportal-auth-persistent";

type AuthState = {
  user: CurrentUser | null;
  isAuthenticated: boolean;
  rememberMe: boolean;
  storageMode: AuthStorageMode | null;
  setAuthenticated: (rememberMe: boolean) => void;
  setUser: (user: CurrentUser | null) => void;
  updateUser: (patch: Partial<CurrentUser>) => void;
  clearAuth: () => void;
  hydrateAuthFromStorage: () => void;
};

function isValidStorageMode(value: unknown): value is AuthStorageMode {
  return value === "session" || value === "persistent";
}

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

function parseStoredPreferences(raw: string, expectedStorageMode: AuthStorageMode): StoredAuthPreferences | null {
  try {
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    if (!parsed || typeof parsed !== "object") {
      return null;
    }

    if (!isValidStorageMode(parsed.storageMode) || parsed.storageMode !== expectedStorageMode) {
      return null;
    }

    if ("accessToken" in parsed || "accessTokenExpiresAt" in parsed || "refreshToken" in parsed) {
      return null;
    }

    return {
      rememberMe: Boolean(parsed.rememberMe),
      storageMode: parsed.storageMode,
      updatedAt: typeof parsed.updatedAt === "number" ? parsed.updatedAt : 0,
    };
  } catch {
    return null;
  }
}

function persistPreferences(preferences: StoredAuthPreferences): void {
  if (typeof window === "undefined") {
    return;
  }

  const targetStorage =
    preferences.storageMode === "persistent" ? window.localStorage : window.sessionStorage;
  const targetKey =
    preferences.storageMode === "persistent" ? AUTH_PERSISTENT_STORAGE_KEY : AUTH_SESSION_STORAGE_KEY;
  const otherStorage =
    preferences.storageMode === "persistent" ? window.sessionStorage : window.localStorage;
  const otherKey =
    preferences.storageMode === "persistent" ? AUTH_SESSION_STORAGE_KEY : AUTH_PERSISTENT_STORAGE_KEY;

  try {
    otherStorage.removeItem(otherKey);
    targetStorage.setItem(
      targetKey,
      JSON.stringify({
        rememberMe: preferences.rememberMe,
        storageMode: preferences.storageMode,
        updatedAt: Date.now(),
      }),
    );
  } catch {
    // ignore quota / private mode
  }
}

function readPreferencesFromStorage(): Pick<AuthState, "rememberMe" | "storageMode"> {
  const base = {
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
      const parsed = parseStoredPreferences(sessionRaw, "session");
      if (parsed) {
        persistPreferences(parsed);
        return { rememberMe: parsed.rememberMe, storageMode: "session" };
      }
      window.sessionStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
    }

    const persistentRaw = window.localStorage.getItem(AUTH_PERSISTENT_STORAGE_KEY);
    if (persistentRaw) {
      const parsed = parseStoredPreferences(persistentRaw, "persistent");
      if (parsed) {
        persistPreferences(parsed);
        return { rememberMe: parsed.rememberMe, storageMode: "persistent" };
      }
      window.localStorage.removeItem(AUTH_PERSISTENT_STORAGE_KEY);
    }
  } catch {
    return base;
  }

  return base;
}

const bootPreferences = readPreferencesFromStorage();

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isAuthenticated: false,
  rememberMe: bootPreferences.rememberMe,
  storageMode: bootPreferences.storageMode,
  setAuthenticated: (rememberMe) =>
    set(() => {
      const storageMode: AuthStorageMode = rememberMe ? "persistent" : "session";
      persistPreferences({ rememberMe, storageMode, updatedAt: Date.now() });
      return {
        isAuthenticated: true,
        rememberMe,
        storageMode,
      };
    }),
  setUser: (user) => set({ user }),
  updateUser: (patch) =>
    set((state) => ({
      user: state.user ? { ...state.user, ...patch } : state.user,
    })),
  clearAuth: () => {
    removeBothAuthKeys();
    clearLegacyKeys();
    set({
      user: null,
      isAuthenticated: false,
      rememberMe: false,
      storageMode: null,
    });
  },
  hydrateAuthFromStorage: () => {
    set(() => ({
      ...readPreferencesFromStorage(),
      user: null,
      isAuthenticated: false,
    }));
  },
}));
