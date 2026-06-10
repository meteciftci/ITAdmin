import type { QueryClient } from "@tanstack/react-query";
import type { NavigateFunction } from "react-router-dom";

import { logout } from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/auth-store";

export { isSelfRoleTarget, normalizeUserId } from "@/features/auth/self-role-target";

export const LOGIN_PERMISSIONS_CHANGED_REASON = "permissionsChanged" as const;

export type LoginRouteReason = "idleTimeout" | typeof LOGIN_PERMISSIONS_CHANGED_REASON;

export async function enforceReloginAfterSelfRoleChange(
  queryClient: QueryClient,
  navigate: NavigateFunction,
): Promise<void> {
  try {
    await logout();
  } catch {
    // best-effort logout; cookies may already be invalid
  }

  useAuthStore.getState().clearAuth();
  queryClient.clear();
  navigate("/login", {
    replace: true,
    state: { reason: LOGIN_PERMISSIONS_CHANGED_REASON },
  });
}
