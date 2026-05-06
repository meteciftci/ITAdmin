import type { LucideIcon } from "lucide-react";
import { ClipboardList, KeyRound, LayoutDashboard, Shield, Users } from "lucide-react";

import type { CurrentUser } from "@/features/auth/types";
import { canAccess } from "@/lib/permissions";

type SidebarItem = {
  titleKey: string;
  to: string;
  icon: LucideIcon;
  visible: boolean;
};

type SidebarGroup = {
  labelKey: string;
  items: SidebarItem[];
};

export const getSidebarGroups = (user: CurrentUser | null): SidebarGroup[] => [
  {
    labelKey: "groups.main",
    items: [
      {
        titleKey: "dashboard",
        to: "/dashboard",
        icon: LayoutDashboard,
        visible: true,
      },
    ],
  },
  {
    labelKey: "groups.administration",
    items: [
      {
        titleKey: "users",
        to: "/users",
        icon: Users,
        visible: canAccess(user, "Users.View"),
      },
      {
        titleKey: "roles",
        to: "/roles",
        icon: Shield,
        visible: canAccess(user, "Roles.View"),
      },
      {
        titleKey: "permissions",
        to: "/permissions",
        icon: KeyRound,
        visible: canAccess(user, "Permissions.View"),
      },
    ],
  },
  {
    labelKey: "groups.monitoring",
    items: [
      {
        titleKey: "auditLogs",
        to: "/audit-logs",
        icon: ClipboardList,
        visible: canAccess(user, "AuditLogs.View"),
      },
    ],
  },
  {
    labelKey: "groups.system",
    items: [],
  },
];
