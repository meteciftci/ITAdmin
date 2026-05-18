import type { LucideIcon } from "lucide-react";
import {
  ClipboardList,
  House,
  KeyRound,
  Network,
  Shield,
  ShieldAlert,
  SlidersHorizontal,
  Users,
} from "lucide-react";

import type { CurrentUser } from "@/features/auth/types";
import { canAccess, canAccessAny } from "@/lib/permissions";

export type SidebarLinkItem = {
  kind: "link";
  titleKey: string;
  to: string;
  icon: LucideIcon;
  visible: boolean;
};

export type SidebarCollapsibleChildItem = {
  titleKey: string;
  to: string;
  icon: LucideIcon;
  visible: boolean;
};

export type SidebarCollapsibleItem = {
  kind: "collapsible";
  titleKey: string;
  routePrefix: string;
  icon: LucideIcon;
  visible: boolean;
  children: SidebarCollapsibleChildItem[];
};

export type SidebarGroupItem = SidebarLinkItem | SidebarCollapsibleItem;

export type SidebarGroup = {
  labelKey: string;
  items: SidebarGroupItem[];
};

export function isSidebarLinkItem(item: SidebarGroupItem): item is SidebarLinkItem {
  return item.kind === "link";
}

export function isSidebarCollapsibleItem(item: SidebarGroupItem): item is SidebarCollapsibleItem {
  return item.kind === "collapsible";
}

export function getVisibleSidebarGroupItems(items: SidebarGroupItem[]): SidebarGroupItem[] {
  return items
    .map((item) => {
      if (isSidebarLinkItem(item)) {
        return item.visible ? item : null;
      }

      const visibleChildren = item.children.filter((child) => child.visible);
      if (!item.visible || visibleChildren.length === 0) {
        return null;
      }

      return { ...item, children: visibleChildren };
    })
    .filter((item): item is SidebarGroupItem => item !== null);
}

export const getSidebarGroups = (user: CurrentUser | null): SidebarGroup[] => [
  {
    labelKey: "groups.main",
    items: [
      {
        kind: "link",
        titleKey: "items.home",
        to: "/home",
        icon: House,
        visible: true,
      },
    ],
  },
  {
    labelKey: "groups.modules",
    items: [
      {
        kind: "collapsible",
        titleKey: "items.adManagement",
        routePrefix: "/ad-management",
        icon: Network,
        visible: canAccess(user, "AdManagement.Users.View"),
        children: [
          {
            titleKey: "items.adManagementUsers",
            to: "/ad-management/users",
            icon: Users,
            visible: canAccess(user, "AdManagement.Users.View"),
          },
        ],
      },
    ],
  },
  {
    labelKey: "groups.administration",
    items: [
      {
        kind: "link",
        titleKey: "items.users",
        to: "/users",
        icon: Users,
        visible: canAccess(user, "Users.View"),
      },
      {
        kind: "link",
        titleKey: "items.roles",
        to: "/roles",
        icon: Shield,
        visible: canAccess(user, "Roles.View"),
      },
      {
        kind: "link",
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
        kind: "link",
        titleKey: "items.auditLogs",
        to: "/audit-logs",
        icon: ClipboardList,
        visible: canAccess(user, "AuditLogs.View"),
      },
      {
        kind: "link",
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
        kind: "link",
        titleKey: "items.settings",
        to: "/settings",
        icon: SlidersHorizontal,
        visible: canAccessAny(user, ["Settings.View", "AdManagement.Settings.View"]),
      },
    ],
  },
];
