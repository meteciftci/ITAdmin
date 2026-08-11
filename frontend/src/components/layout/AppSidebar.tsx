import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { useLayoutShell } from "@/components/layout/useLayoutShell";
import {
  getSidebarGroups,
  getVisibleSidebarGroupItems,
  isSidebarLinkItem,
  type SidebarCollapsibleItem,
  type SidebarLinkItem,
} from "@/components/layout/sidebar-items";
import { Tooltip } from "@/components/ui/tooltip";
import { buttonVariants } from "@/components/ui/button-variants";
import { Separator } from "@/components/ui/separator";
import {
  AD_MANAGEMENT_SETTINGS_QUERY_KEY,
  getAdManagementSettings,
} from "@/features/ad-management/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { useBrandingSettings } from "@/hooks/useBrandingSettings";
import { resolveApiAssetUrl } from "@/lib/api-client";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { PermissionCodes } from "@/lib/permission-codes";

function isRouteActive(pathname: string, to: string) {
  return pathname === to || pathname.startsWith(`${to}/`);
}

function isCollapsibleActive(pathname: string, routePrefix: string) {
  return pathname === routePrefix || pathname.startsWith(`${routePrefix}/`);
}

export function AppSidebar() {
  const { t } = useTranslation(["navigation"]);
  const location = useLocation();
  const user = useAuthStore((state) => state.user);
  const { sidebarCollapsed, mobileSidebarOpen, setMobileSidebarOpen } = useLayoutShell();

  const canViewAdManagementModule =
    canAccess(user, PermissionCodes.AdManagement.Users.View)
    || canAccess(user, PermissionCodes.AdManagement.Groups.View)
    || canAccess(user, PermissionCodes.AdManagement.Computers.View)
    || canAccess(user, PermissionCodes.AdManagement.OrganizationalUnits.View);
  const adManagementSettingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getAdManagementSettings,
    enabled: canViewAdManagementModule,
    staleTime: 60_000,
  });

  const groups = getSidebarGroups(user, {
    isConfigured: adManagementSettingsQuery.data?.isConfigured ?? false,
    isEnabled: adManagementSettingsQuery.data?.isEnabled ?? false,
    isLoading: adManagementSettingsQuery.isLoading,
  });
  const { data: branding } = useBrandingSettings();
  const appName = branding.applicationName || "ITAdmin";
  const resolvedLogoUrl = resolveApiAssetUrl(branding.logoUrl);
  const initials = appName
    .split(" ")
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  const [expandedMenus, setExpandedMenus] = useState<Record<string, boolean>>({});

  const linkClassName = (isActive: boolean, collapsed: boolean) =>
    cn(
      buttonVariants({ variant: "ghost" }),
      "w-full gap-2 transition-colors",
      isActive
        ? "bg-sidebar-primary text-sidebar-primary-foreground hover:bg-sidebar-primary/90 hover:text-sidebar-primary-foreground"
        : "text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
      collapsed ? "justify-center px-0" : "justify-start",
    );

  const renderLinkItem = (item: SidebarLinkItem) => {
    const Icon = item.icon;
    const isActive = isRouteActive(location.pathname, item.to);
    const itemTitle = t(item.titleKey);
    const link = (
      <Link
        key={item.to}
        to={item.to}
        onClick={() => setMobileSidebarOpen(false)}
        className={linkClassName(isActive, sidebarCollapsed)}
      >
        <Icon className="size-4 shrink-0" />
        {!sidebarCollapsed ? <span className="truncate">{itemTitle}</span> : null}
      </Link>
    );

    return (
      <Tooltip key={item.to} text={itemTitle} disabled={!sidebarCollapsed}>
        {link}
      </Tooltip>
    );
  };

  const renderCollapsibleItem = (item: SidebarCollapsibleItem) => {
    const Icon = item.icon;
    const parentTitle = t(item.titleKey);
    const isParentActive = isCollapsibleActive(location.pathname, item.routePrefix);
    const isExpanded = isParentActive
      ? expandedMenus[item.routePrefix] !== false
      : Boolean(expandedMenus[item.routePrefix]);
    const firstChild = item.children[0];

    if (sidebarCollapsed) {
      const collapsedTarget = firstChild?.to ?? item.routePrefix;
      const collapsedTitle = firstChild ? `${parentTitle} · ${t(firstChild.titleKey)}` : parentTitle;
      const ChildIcon = firstChild?.icon ?? Icon;

      return (
        <Tooltip key={item.routePrefix} text={collapsedTitle}>
          <Link
            to={collapsedTarget}
            onClick={() => setMobileSidebarOpen(false)}
            className={linkClassName(isParentActive, true)}
          >
            <ChildIcon className="size-4 shrink-0" />
          </Link>
        </Tooltip>
      );
    }

    return (
      <div key={item.routePrefix} className="space-y-1">
        <button
          type="button"
          onClick={() =>
            setExpandedMenus((current) => ({
              ...current,
              [item.routePrefix]: !isExpanded,
            }))
          }
          className={cn(
            linkClassName(isParentActive, false),
            "font-medium",
          )}
          aria-expanded={isExpanded}
        >
          <Icon className="size-4 shrink-0" />
          <span className="truncate">{parentTitle}</span>
          <ChevronDown
            className={cn(
              "ml-auto size-4 shrink-0 transition-transform",
              isExpanded ? "rotate-180" : "",
            )}
          />
        </button>
        {isExpanded ? (
          <div className="ml-3 space-y-1 border-l border-border/70 pl-2">
            {item.children.map((child) => {
              const ChildIcon = child.icon;
              const childTitle = t(child.titleKey);
              const isChildActive = isRouteActive(location.pathname, child.to);

              return (
                <Link
                  key={child.to}
                  to={child.to}
                  onClick={() => setMobileSidebarOpen(false)}
                  className={cn(
                    linkClassName(isChildActive, false),
                    "text-sm",
                  )}
                >
                  <ChildIcon className="size-4 shrink-0" />
                  <span className="truncate">{childTitle}</span>
                </Link>
              );
            })}
          </div>
        ) : null}
      </div>
    );
  };

  return (
    <>
      {mobileSidebarOpen ? (
        <button
          type="button"
          className="fixed inset-0 z-40 bg-black/40 lg:hidden"
          onClick={() => setMobileSidebarOpen(false)}
          aria-label={t("navigation:close")}
        />
      ) : null}
      <aside
        id="app-navigation"
        className={cn(
          "fixed inset-y-0 left-0 z-50 flex h-screen w-[min(280px,calc(100vw-2rem))] flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground shadow-2xl transition-transform lg:static lg:z-auto lg:h-full lg:w-[280px] lg:shrink-0 lg:shadow-none",
          sidebarCollapsed && "lg:hidden",
          mobileSidebarOpen ? "translate-x-0" : "-translate-x-full lg:translate-x-0",
        )}
      >
        <div className="flex h-16 items-center border-b border-sidebar-border px-4">
          <div className="flex min-w-0 items-center gap-3">
            {resolvedLogoUrl ? (
              <img src={resolvedLogoUrl} alt={appName} className="size-9 rounded-lg object-contain" />
            ) : (
              <div className="flex size-9 items-center justify-center rounded-lg bg-sidebar-primary text-xs font-bold text-sidebar-primary-foreground shadow-sm">
                {initials || "SP"}
              </div>
            )}
            <div className="min-w-0 leading-tight">
              <p className="truncate text-sm font-semibold text-sidebar-foreground">{appName}</p>
              <p className="truncate text-xs text-sidebar-foreground/55">
                {t("navigation:subtitle")}
              </p>
            </div>
          </div>
        </div>
        <Separator className="bg-sidebar-border" />
        <nav className="flex-1 space-y-5 overflow-y-auto p-3.5">
          {groups.map((group) => {
            const visibleItems = getVisibleSidebarGroupItems(group.items);
            if (!visibleItems.length) return null;
            const groupLabel = t(group.labelKey);

            return (
              <div key={group.labelKey} className="space-y-1">
                <p className="px-3 text-[0.6875rem] font-semibold uppercase tracking-[0.14em] text-sidebar-foreground/45">
                  {groupLabel}
                </p>
                {visibleItems.map((item) =>
                  isSidebarLinkItem(item)
                    ? renderLinkItem(item)
                    : renderCollapsibleItem(item),
                )}
              </div>
            );
          })}
        </nav>
      </aside>
    </>
  );
}
