import { NavLink, Navigate, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { ReactNode } from "react";

import { LazyRoute } from "@/app/route-helpers";
import { AppLayout } from "@/components/layout/AppLayout";
import { useAuthStore } from "@/features/auth/auth-store";
import { PermissionCodes } from "@/lib/permission-codes";
import { canAccess, canAccessAny } from "@/lib/permissions";
import { getErrorRoutePath } from "@/lib/route-error";
import { cn } from "@/lib/utils";

type NavigationTab = {
  path: string;
  labelKey: string;
  visible: boolean;
};

function RouteTabs({ tabs }: { tabs: NavigationTab[] }) {
  const { t } = useTranslation("dnsManagement");

  return (
    <nav
      aria-label={t("navigationTabs.label")}
      className="flex flex-wrap gap-2 border-b pb-3"
    >
      {tabs
        .filter((tab) => tab.visible)
        .map((tab) => (
          <NavLink
            key={tab.path}
            to={tab.path}
            className={({ isActive }) =>
              cn(
                "inline-flex items-center rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
                isActive
                  ? "bg-primary text-primary-foreground"
                  : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
              )
            }
          >
            {t(tab.labelKey)}
          </NavLink>
        ))}
    </nav>
  );
}

export function DnsModuleSettingsTabs() {
  const user = useAuthStore((state) => state.user);
  return (
    <RouteTabs
      tabs={[
        {
          path: "/settings/modules/dns-management",
          labelKey: "navigationTabs.settings.general",
          visible: canAccess(
            user,
            PermissionCodes.DnsManagement.ManageSettings,
          ),
        },
        {
          path: "/settings/modules/dns-management/servers",
          labelKey: "navigationTabs.settings.servers",
          visible: canAccess(user, PermissionCodes.DnsManagement.Servers.View),
        },
        {
          path: "/settings/modules/dns-management/server-settings",
          labelKey: "navigationTabs.settings.serverSettings",
          visible: canAccessAny(user, [
            PermissionCodes.DnsManagement.ManageServerSettings,
            PermissionCodes.DnsManagement.ClearCache,
          ]),
        },
      ]}
    />
  );
}

export function DnsModuleSettingsPageShell({
  children,
}: {
  children: ReactNode;
}) {
  return (
    <AppLayout>
      <div className="space-y-4">
        <DnsModuleSettingsTabs />
        <LazyRoute>{children}</LazyRoute>
      </div>
    </AppLayout>
  );
}

const advancedPermissions = [
  PermissionCodes.DnsManagement.ManagePolicies,
  PermissionCodes.DnsManagement.ManageDnssec,
  PermissionCodes.DnsManagement.ManageScavenging,
  PermissionCodes.DnsManagement.ManageNetworkConfiguration,
  PermissionCodes.DnsManagement.ManageZoneTransfers,
  PermissionCodes.DnsManagement.ManageZoneDelegations,
];

export function DnsAdvancedManagementTabs() {
  const user = useAuthStore((state) => state.user);
  return (
    <RouteTabs
      tabs={[
        {
          path: "/dns-management/policies",
          labelKey: "navigationTabs.advanced.policies",
          visible: canAccess(
            user,
            PermissionCodes.DnsManagement.ManagePolicies,
          ),
        },
        {
          path: "/dns-management/dnssec",
          labelKey: "navigationTabs.advanced.dnssec",
          visible: canAccess(user, PermissionCodes.DnsManagement.ManageDnssec),
        },
        {
          path: "/dns-management/scavenging",
          labelKey: "navigationTabs.advanced.scavenging",
          visible: canAccess(
            user,
            PermissionCodes.DnsManagement.ManageScavenging,
          ),
        },
        {
          path: "/dns-management/network-configuration",
          labelKey: "navigationTabs.advanced.network",
          visible: canAccess(
            user,
            PermissionCodes.DnsManagement.ManageNetworkConfiguration,
          ),
        },
        {
          path: "/dns-management/zone-transfers",
          labelKey: "navigationTabs.advanced.transfers",
          visible: canAccess(
            user,
            PermissionCodes.DnsManagement.ManageZoneTransfers,
          ),
        },
        {
          path: "/dns-management/zone-delegations",
          labelKey: "navigationTabs.advanced.delegations",
          visible: canAccess(
            user,
            PermissionCodes.DnsManagement.ManageZoneDelegations,
          ),
        },
      ]}
    />
  );
}

export function DnsAdvancedManagementPageShell({
  children,
}: {
  children: ReactNode;
}) {
  return (
    <AppLayout>
      <div className="space-y-4">
        <DnsAdvancedManagementTabs />
        <LazyRoute>{children}</LazyRoute>
      </div>
    </AppLayout>
  );
}

export function DnsAdvancedManagementRedirectPage() {
  const location = useLocation();
  const user = useAuthStore((state) => state.user);
  const paths = [
    "/dns-management/policies",
    "/dns-management/dnssec",
    "/dns-management/scavenging",
    "/dns-management/network-configuration",
    "/dns-management/zone-transfers",
    "/dns-management/zone-delegations",
  ];
  const index = advancedPermissions.findIndex((permission) =>
    canAccess(user, permission),
  );

  if (index >= 0) return <Navigate to={paths[index]} replace />;

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
