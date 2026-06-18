import { Navigate } from "react-router-dom";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";

export function NotificationSettingsRedirectPage() {
  const user = useAuthStore((state) => state.user);

  if (canAccess(user, PermissionCodes.NotificationProviders.View)) {
    return <Navigate to="/settings/notifications/providers" replace />;
  }

  if (canAccess(user, PermissionCodes.NotificationTemplates.View)) {
    return <Navigate to="/settings/notifications/templates" replace />;
  }

  return <Navigate to="/settings/notifications/providers" replace />;
}
