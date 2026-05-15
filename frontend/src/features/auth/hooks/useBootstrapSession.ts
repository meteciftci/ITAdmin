import axios from "axios";
import { useQuery } from "@tanstack/react-query";

import { getCurrentUser, refreshToken } from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { i18n, normalizeLanguage } from "@/app/i18n";

export type UseBootstrapSessionResult =
  | { status: "authenticated" }
  | { status: "pending" }
  | { status: "unauthenticated" }
  | { status: "error"; error: unknown };

const isUnauthorized = (error: unknown): boolean =>
  axios.isAxiosError(error) && error.response?.status === 401;

async function tryEstablishSession(
  setUser: ReturnType<typeof useAuthStore.getState>["setUser"],
  setAuthenticated: ReturnType<typeof useAuthStore.getState>["setAuthenticated"],
  rememberMe: boolean,
): Promise<"authenticated" | "unauthenticated"> {
  try {
    const user = await getCurrentUser();
    setUser(user);
    setAuthenticated(rememberMe);
    void i18n.changeLanguage(normalizeLanguage(user.preferredLanguage));
    return "authenticated";
  } catch (meError) {
    if (!isUnauthorized(meError)) {
      throw meError;
    }
  }

  try {
    const refresh = await refreshToken();
    if (!refresh.isSuccess) {
      return "unauthenticated";
    }

    const user = await getCurrentUser();
    setUser(user);
    setAuthenticated(rememberMe);
    void i18n.changeLanguage(normalizeLanguage(user.preferredLanguage));
    return "authenticated";
  } catch (refreshError) {
    if (!isUnauthorized(refreshError)) {
      throw refreshError;
    }
    return "unauthenticated";
  }
}

/**
 * Cookie-based session bootstrap: /auth/me first, then cookie refresh + /auth/me on 401.
 */
export function useBootstrapSession(): UseBootstrapSessionResult {
  const rememberMe = useAuthStore((state) => state.rememberMe);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const setUser = useAuthStore((state) => state.setUser);
  const setAuthenticated = useAuthStore((state) => state.setAuthenticated);
  const clearAuth = useAuthStore((state) => state.clearAuth);

  const bootstrapQuery = useQuery({
    queryKey: ["auth", "bootstrap"],
    queryFn: async (): Promise<"authenticated" | "unauthenticated"> => {
      const outcome = await tryEstablishSession(setUser, setAuthenticated, rememberMe);
      if (outcome === "unauthenticated") {
        clearAuth();
      }
      return outcome;
    },
    enabled: !isAuthenticated,
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: 0,
    retry: false,
  });

  if (isAuthenticated) {
    return { status: "authenticated" };
  }

  if (bootstrapQuery.isPending || bootstrapQuery.isFetching) {
    return { status: "pending" };
  }

  if (bootstrapQuery.isError) {
    return { status: "error", error: bootstrapQuery.error };
  }

  if (bootstrapQuery.data === "authenticated") {
    return { status: "authenticated" };
  }

  return { status: "unauthenticated" };
}
