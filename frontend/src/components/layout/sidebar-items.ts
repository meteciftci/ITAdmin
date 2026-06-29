import type { LucideIcon } from "lucide-react";
import {
  Archive,
  Activity,
  BellRing,
  Boxes,
  ClipboardList,
  Inbox,
  House,
  KeyRound,
  ListTree,
  Monitor,
  Network,
  Shield,
  ShieldAlert,
  SlidersHorizontal,
  Users,
  FolderTree,
  FileKey,
} from "lucide-react";

import type { CurrentUser } from "@/features/auth/types";
import { canAccess, canAccessAny } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";

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

function isAdManagementModuleOperational(
  moduleState?: AdManagementModuleSidebarState,
): boolean {
  if (!moduleState || moduleState.isLoading) {
    return false;
  }

  return moduleState.isConfigured && moduleState.isEnabled;
}

function isAdManagementUsersVisible(
  user: CurrentUser | null,
  moduleState?: AdManagementModuleSidebarState,
): boolean {
  if (!canAccess(user, PermissionCodes.AdManagement.Users.View)) {
    return false;
  }

  return isAdManagementModuleOperational(moduleState);
}

function isAdManagementGroupsVisible(
  user: CurrentUser | null,
  moduleState?: AdManagementModuleSidebarState,
): boolean {
  if (!canAccess(user, PermissionCodes.AdManagement.Groups.View)) {
    return false;
  }

  return isAdManagementModuleOperational(moduleState);
}

function isAdManagementComputersVisible(
  user: CurrentUser | null,
  moduleState?: AdManagementModuleSidebarState,
): boolean {
  if (!canAccess(user, PermissionCodes.AdManagement.Computers.View)) {
    return false;
  }

  return isAdManagementModuleOperational(moduleState);
}

function isAdManagementOrganizationalUnitsVisible(
  user: CurrentUser | null,
  moduleState?: AdManagementModuleSidebarState,
): boolean {
  if (!canAccess(user, PermissionCodes.AdManagement.OrganizationalUnits.View)) {
    return false;
  }

  return isAdManagementModuleOperational(moduleState);
}

function isAdManagementDeletedObjectsVisible(
  user: CurrentUser | null,
  moduleState?: AdManagementModuleSidebarState,
): boolean {
  if (!canAccess(user, PermissionCodes.AdManagement.DeletedObjects.View)) {
    return false;
  }

  return isAdManagementModuleOperational(moduleState);
}

function isAdManagementSectionVisible(
  user: CurrentUser | null,
  moduleState?: AdManagementModuleSidebarState,
): boolean {
  return isAdManagementUsersVisible(user, moduleState)
    || isAdManagementGroupsVisible(user, moduleState)
    || isAdManagementComputersVisible(user, moduleState)
    || isAdManagementOrganizationalUnitsVisible(user, moduleState)
    || isAdManagementDeletedObjectsVisible(user, moduleState);
}

function isLicenseManagementSectionVisible(user: CurrentUser | null): boolean {
  return canAccess(user, PermissionCodes.LicenseManagement.View);
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
        visible: isAdManagementSectionVisible(user, adManagementModule),
        children: [
          {
            titleKey: "items.adManagementUsers",
            to: "/ad-management/users",
            icon: Users,
            visible: isAdManagementUsersVisible(user, adManagementModule),
          },
          {
            titleKey: "items.adManagementGroups",
            to: "/ad-management/groups",
            icon: Shield,
            visible: isAdManagementGroupsVisible(user, adManagementModule),
          },
          {
            titleKey: "items.adManagementComputers",
            to: "/ad-management/computers",
            icon: Monitor,
            visible: isAdManagementComputersVisible(user, adManagementModule),
          },
          {
            titleKey: "items.adManagementOrganizationalUnits",
            to: "/ad-management/organizational-units",
            icon: FolderTree,
            visible: isAdManagementOrganizationalUnitsVisible(user, adManagementModule),
          },
          {
            titleKey: "items.adManagementDeletedObjects",
            to: "/ad-management/deleted-objects",
            icon: Archive,
            visible: isAdManagementDeletedObjectsVisible(user, adManagementModule),
          },
        ],
      },
      {
        kind: "collapsible",
        titleKey: "items.licenseManagement",
        routePrefix: "/license-management",
        icon: FileKey,
        visible: isLicenseManagementSectionVisible(user),
        children: [
          {
            titleKey: "items.licenseManagementOverview",
            to: "/license-management/overview",
            icon: House,
            visible: isLicenseManagementSectionVisible(user),
          },
          {
            titleKey: "items.licenseManagementCompanies",
            to: "/license-management/companies",
            icon: Users,
            visible: isLicenseManagementSectionVisible(user),
          },
          {
            titleKey: "items.licenseManagementProducts",
            to: "/license-management/products",
            icon: Boxes,
            visible: isLicenseManagementSectionVisible(user),
          },
          {
            titleKey: "items.licenseManagementPurchases",
            to: "/license-management/purchases",
            icon: ClipboardList,
            visible: isLicenseManagementSectionVisible(user),
          },
          {
            titleKey: "items.licenseManagementRequests",
            to: "/license-management/requests",
            icon: ListTree,
            visible: isLicenseManagementSectionVisible(user),
          },
          {
            titleKey: "items.licenseManagementPackages",
            to: "/license-management/packages",
            icon: KeyRound,
            visible: isLicenseManagementSectionVisible(user),
          },
        ],
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
        visible: canAccess(user, PermissionCodes.AuditLogs.View),
      },
      {
        kind: "link",
        titleKey: "items.securityLogs",
        to: "/security-logs",
        icon: ShieldAlert,
        visible: canAccess(user, PermissionCodes.SecurityLogs.View),
      },
      {
        kind: "collapsible",
        titleKey: "items.moduleLogs",
        routePrefix: "/monitoring/module-logs",
        icon: ListTree,
        visible: canAccess(user, PermissionCodes.AdOperationLogs.View),
        children: [
          {
            titleKey: "items.adOperationLogs",
            to: "/monitoring/module-logs/ad-operation-logs",
            icon: Activity,
            visible: canAccess(user, PermissionCodes.AdOperationLogs.View),
          },
        ],
      },
      {
        kind: "link",
        titleKey: "items.notificationOutbox",
        to: "/notification-outbox",
        icon: Inbox,
        visible: canAccess(user, PermissionCodes.NotificationOutbox.View),
      },
    ],
  },
  {
    labelKey: "groups.system",
    items: [
      {
        kind: "link",
        titleKey: "items.users",
        to: "/users",
        icon: Users,
        visible: canAccess(user, PermissionCodes.Users.View),
      },
      {
        kind: "link",
        titleKey: "items.roles",
        to: "/roles",
        icon: Shield,
        visible: canAccess(user, PermissionCodes.Roles.View),
      },
      {
        kind: "link",
        titleKey: "items.permissions",
        to: "/permissions",
        icon: KeyRound,
        visible: canAccess(user, PermissionCodes.Permissions.View),
      },
      {
        kind: "collapsible",
        titleKey: "items.settings",
        routePrefix: "/settings",
        icon: SlidersHorizontal,
        visible: canAccessAny(user, [
          PermissionCodes.Settings.View,
          PermissionCodes.NotificationProviders.View,
          PermissionCodes.NotificationTemplates.View,
          PermissionCodes.AdManagement.Settings.View,
          PermissionCodes.LicenseManagement.ManageSettings,
        ]),
        children: [
          {
            titleKey: "items.applicationSettings",
            to: "/settings/application",
            icon: SlidersHorizontal,
            visible: canAccess(user, PermissionCodes.Settings.View),
          },
          {
            titleKey: "items.notificationSettings",
            to: "/settings/notifications",
            icon: BellRing,
            visible: canAccessAny(user, [
              PermissionCodes.NotificationProviders.View,
              PermissionCodes.NotificationTemplates.View,
            ]),
          },
          {
            titleKey: "items.moduleSettings",
            to: "/settings/modules",
            icon: Boxes,
            visible: canAccessAny(user, [
              PermissionCodes.AdManagement.Settings.View,
              PermissionCodes.LicenseManagement.ManageSettings,
            ]),
          },
        ],
      },
    ],
  },
];
