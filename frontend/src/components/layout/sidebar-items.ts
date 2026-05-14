import type { LucideIcon } from "lucide-react";
import {
  ClipboardList,
  House,
  KeyRound,
  Shield,
  ShieldAlert,
  SlidersHorizontal,
  Users,
} from "lucide-react";

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
        titleKey: "items.home",
        to: "/home",
        icon: House,
        visible: true,
      },
    ],
  },
  {
    labelKey: "groups.administration",
    items: [
      {
        titleKey: "items.users",
        to: "/users",
        icon: Users,
        visible: canAccess(user, "Users.View"),
      },
      {
        titleKey: "items.roles",
        to: "/roles",
        icon: Shield,
        visible: canAccess(user, "Roles.View"),
      },
      {
        titleKey: "items.permissions",
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
        titleKey: "items.auditLogs",
        to: "/audit-logs",
        icon: ClipboardList,
        visible: canAccess(user, "AuditLogs.View"),
      },
      {
        titleKey: "items.securityLogs",
        to: "/security-logs",
        icon: ShieldAlert,
        visible: canAccess(user, "SecurityLogs.View"),
      },
    ],
  },
  {
    labelKey: "groups.system",
    items: [
      {
        titleKey: "items.settings",
        to: "/settings",
        icon: SlidersHorizontal,
        visible: canAccess(user, "Settings.View"),
      },
    ],
  },
];
