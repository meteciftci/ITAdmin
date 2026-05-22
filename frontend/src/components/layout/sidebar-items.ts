import type { LucideIcon } from "lucide-react";
import {
  Boxes,
  ClipboardList,
  Inbox,
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

export type AdManagementModuleSidebarState = {
  isConfigured: boolean;
  isEnabled: boolean;
  isLoading?: boolean;
};

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

function isAdManagementOperationsVisible(
  user: CurrentUser | null,
  moduleState?: AdManagementModuleSidebarState,
): boolean {
  if (!canAccess(user, "AdManagement.Users.View")) {
    return false;
  }

  if (!moduleState || moduleState.isLoading) {
    return false;
  }

  return moduleState.isConfigured && moduleState.isEnabled;
}

export const getSidebarGroups = (
  user: CurrentUser | null,
  adManagementModule?: AdManagementModuleSidebarState,
): SidebarGroup[] => [
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
        visible: isAdManagementOperationsVisible(user, adManagementModule),
        children: [
          {
            titleKey: "items.adManagementUsers",
            to: "/ad-management/users",
            icon: Users,
            visible: isAdManagementOperationsVisible(user, adManagementModule),
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
      {
        kind: "link",
        titleKey: "items.notificationOutbox",
        to: "/notification-outbox",
        icon: Inbox,
        visible: canAccess(user, "NotificationOutbox.View"),
      },
    ],
  },
  {
    labelKey: "groups.system",
    items: [
      {
        kind: "collapsible",
        titleKey: "items.settings",
        routePrefix: "/settings",
        icon: SlidersHorizontal,
        visible: canAccessAny(user, [
          "Settings.View",
          "NotificationProviders.View",
          "NotificationTemplates.View",
          "AdManagement.Settings.View",
        ]),
        children: [
          {
            titleKey: "items.applicationSettings",
            to: "/settings/application",
            icon: SlidersHorizontal,
            visible: canAccess(user, "Settings.View"),
          },
          {
            titleKey: "items.notificationProviders",
            to: "/settings/notification-providers",
            icon: SlidersHorizontal,
            visible: canAccess(user, "NotificationProviders.View"),
          },
          {
            titleKey: "items.notificationTemplates",
            to: "/settings/notification-templates",
            icon: SlidersHorizontal,
            visible: canAccess(user, "NotificationTemplates.View"),
          },
          {
            titleKey: "items.moduleSettings",
            to: "/settings/modules",
            icon: Boxes,
            visible: canAccess(user, "AdManagement.Settings.View"),
          },
        ],
      },
    ],
  },
];
