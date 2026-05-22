import { NavLink } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils";
import { canAccess } from "@/lib/permissions";
import { useAuthStore } from "@/features/auth/auth-store";

type NotificationSettingsTabsProps = {
  activeTab: "providers" | "templates";
};

export function NotificationSettingsTabs({ activeTab }: NotificationSettingsTabsProps) {
  const { t } = useTranslation(["notificationSettings"]);
  const user = useAuthStore((state) => state.user);
  const canViewProviders = canAccess(user, "NotificationProviders.View");
  const canViewTemplates = canAccess(user, "NotificationTemplates.View");

  const tabClassName = (isActive: boolean) =>
    cn(
      "inline-flex items-center rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
      isActive
        ? "bg-primary text-primary-foreground"
        : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
    );

  return (
    <div className="flex flex-wrap gap-2 border-b pb-3">
      {canViewProviders ? (
        <NavLink
          to="/settings/notifications/providers"
          className={({ isActive }) => tabClassName(isActive || activeTab === "providers")}
        >
          {t("notificationSettings:tabs.providers")}
        </NavLink>
      ) : null}
      {canViewTemplates ? (
        <NavLink
          to="/settings/notifications/templates"
          className={({ isActive }) => tabClassName(isActive || activeTab === "templates")}
        >
          {t("notificationSettings:tabs.templates")}
        </NavLink>
      ) : null}
    </div>
  );
}
