import { Navigate, useLocation } from "react-router-dom";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { getErrorRoutePath } from "@/lib/route-error";

export function SettingsRedirectPage() {
  const location = useLocation();
  const user = useAuthStore((state) => state.user);

  if (canAccess(user, "Settings.View")) {
    return <Navigate to="/settings/application" replace />;
  }

  if (canAccess(user, "NotificationProviders.View")) {
    return <Navigate to="/settings/notification-providers" replace />;
  }

  if (canAccess(user, "AdManagement.Settings.View")) {
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
