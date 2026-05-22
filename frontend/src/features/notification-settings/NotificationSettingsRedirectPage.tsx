import { Navigate } from "react-router-dom";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";

export function NotificationSettingsRedirectPage() {
  const user = useAuthStore((state) => state.user);

  if (canAccess(user, "NotificationProviders.View")) {
    return <Navigate to="/settings/notifications/providers" replace />;
  }

  if (canAccess(user, "NotificationTemplates.View")) {
    return <Navigate to="/settings/notifications/templates" replace />;
  }

  return <Navigate to="/settings/notifications/providers" replace />;
}
