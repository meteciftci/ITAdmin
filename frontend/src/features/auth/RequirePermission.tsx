import type { ReactNode } from "react";

import { Navigate } from "react-router-dom";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";

type RequirePermissionProps = {
  permission: string;
  children: ReactNode;
};

export function RequirePermission({
  permission,
  children,
}: RequirePermissionProps) {
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);

  if (!accessToken) {
    return <Navigate to="/login" replace />;
  }

  if (!user) {
    return <div className="p-6 text-sm text-muted-foreground">Loading...</div>;
  }

  if (!canAccess(user, permission)) {
    return (
      <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-6 text-sm text-destructive">
        403 - You do not have permission to access this page.
      </div>
    );
  }

  return <>{children}</>;
}
