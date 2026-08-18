import { Navigate, useLocation } from "react-router-dom";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { getErrorRoutePath } from "@/lib/route-error";
import { PermissionCodes } from "@/lib/permission-codes";

export function SettingsRedirectPage() {
  const location = useLocation();
  const user = useAuthStore((state) => state.user);

  if (canAccess(user, PermissionCodes.Settings.View)) {
    return <Navigate to="/settings/application" replace />;
  }

  if (canAccess(user, PermissionCodes.SystemUpdates.View)) {
    return <Navigate to="/settings/updates" replace />;
  }

  if (
    canAccess(user, PermissionCodes.NotificationProviders.View) ||
    canAccess(user, PermissionCodes.NotificationTemplates.View)
  ) {
    return <Navigate to="/settings/notifications" replace />;
  }

  if (canAccess(user, PermissionCodes.AdManagement.Settings.View)) {
    return <Navigate to="/settings/modules/ad-management" replace />;
  }

  return (
    <Navigate
      to={getErrorRoutePath("FORBIDDEN")}
      replace
      state={{
        code: "FORBIDDEN",
        kind: "forbidden" as const,
        status: 403,
        titleKey: "errors:api.forbidden.title",
        descriptionKey: "errors:api.forbidden.description",
        fromPath: location.pathname,
        retryPath: location.pathname,
      }}
    />
  );
}
