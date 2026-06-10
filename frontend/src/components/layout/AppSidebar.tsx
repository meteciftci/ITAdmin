import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown, ChevronLeft, ChevronRight } from "lucide-react";
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
  const {
    sidebarCollapsed,
    setSidebarCollapsed,
    mobileSidebarOpen,
    setMobileSidebarOpen,
  } = useLayoutShell();

  const canViewAdManagementModule =
    canAccess(user, "AdManagement.Users.View")
    || canAccess(user, "AdManagement.Groups.View")
    || canAccess(user, "AdManagement.Computers.View");
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
  const appName = branding.applicationName || "SAS Portal v2";
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
          aria-label="close sidebar"
        />
      ) : null}
      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-50 flex h-screen flex-col border-r bg-card transition-all lg:static lg:z-auto lg:h-full lg:shrink-0",
          sidebarCollapsed ? "w-[72px]" : "w-[260px]",
          mobileSidebarOpen ? "translate-x-0" : "-translate-x-full lg:translate-x-0",
        )}
      >
        <div className="flex h-16 items-center justify-between border-b px-3">
          <div className={cn("flex min-w-0 items-center gap-2", sidebarCollapsed ? "justify-center" : "")}>
            {resolvedLogoUrl ? (
              <img src={resolvedLogoUrl} alt={appName} className="size-8 rounded-md object-contain" />
            ) : (
              <div className="flex size-8 items-center justify-center rounded-md bg-muted text-xs font-semibold text-muted-foreground">
                {initials || "SP"}
              </div>
            )}
            {!sidebarCollapsed ? (
              <p className="truncate text-sm font-semibold tracking-wide text-muted-foreground">{appName}</p>
            ) : null}
          </div>
          <button
            type="button"
            onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
            className={cn(
              buttonVariants({ variant: "ghost", size: "icon-sm" }),
              "hidden lg:inline-flex",
            )}
            aria-label="toggle sidebar"
          >
            {sidebarCollapsed ? <ChevronRight className="size-4" /> : <ChevronLeft className="size-4" />}
          </button>
        </div>
        <Separator />
        <nav className="flex-1 space-y-4 overflow-y-auto p-3">
          {groups.map((group) => {
            const visibleItems = getVisibleSidebarGroupItems(group.items);
            if (!visibleItems.length) return null;
            const groupLabel = t(group.labelKey);

            return (
              <div key={group.labelKey} className="space-y-1">
                {!sidebarCollapsed ? (
                  <p className="px-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    {groupLabel}
                  </p>
                ) : null}
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
