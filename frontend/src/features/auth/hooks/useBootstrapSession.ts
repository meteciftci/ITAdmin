import { useQuery } from "@tanstack/react-query";

import { refreshToken } from "@/features/auth/api";
import { isFutureExpiry, useAuthStore } from "@/features/auth/auth-store";

export type UseBootstrapSessionResult =
  | { status: "authenticated" }
  | { status: "pending" }
  | { status: "unauthenticated" };

/**
 * One-shot bootstrap when access token is missing: tries cookie-based silent refresh.
 * Does not run when an access token is already present.
 */
export function useBootstrapSession(): UseBootstrapSessionResult {
  const accessToken = useAuthStore((state) => state.accessToken);
  const rememberMe = useAuthStore((state) => state.rememberMe);
  const setTokens = useAuthStore((state) => state.setTokens);
  const clearAuth = useAuthStore((state) => state.clearAuth);

  const bootstrapQuery = useQuery({
    queryKey: ["auth", "bootstrap-refresh"],
    queryFn: async (): Promise<"authenticated" | "unauthenticated"> => {
      try {
        const data = await refreshToken();
        if (!data.isSuccess || !data.accessToken?.trim()) {
          clearAuth();
          return "unauthenticated";
        }

        if (!isFutureExpiry(data.accessTokenExpiresAt)) {
          clearAuth();
          return "unauthenticated";
        }

        setTokens(
          {
            accessToken: data.accessToken,
            accessTokenExpiresAt: data.accessTokenExpiresAt,
          },
          rememberMe,
        );

        return "authenticated";
      } catch {
        clearAuth();
        return "unauthenticated";
      }
    },
    enabled: !accessToken,
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: 0,
    retry: false,
  });

  if (accessToken) {
    return { status: "authenticated" };
  }

  if (bootstrapQuery.isPending || bootstrapQuery.isFetching) {
    return { status: "pending" };
  }

  if (bootstrapQuery.data === "authenticated") {
    return { status: "authenticated" };
  }

  return { status: "unauthenticated" };
}
