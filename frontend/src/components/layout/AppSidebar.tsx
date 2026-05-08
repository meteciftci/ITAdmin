import { ChevronLeft, ChevronRight } from "lucide-react";
import { Link, useLocation } from "react-router-dom";

import { useLayoutShell } from "@/components/layout/useLayoutShell";
import { getSidebarGroups } from "@/components/layout/sidebar-items";
import { Tooltip } from "@/components/ui/tooltip";
import { buttonVariants } from "@/components/ui/button-variants";
import { Separator } from "@/components/ui/separator";
import { useAuthStore } from "@/features/auth/auth-store";
import { useBrandingSettings } from "@/hooks/useBrandingSettings";
import { cn } from "@/lib/utils";
import { useTranslation } from "react-i18next";

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

  const groups = getSidebarGroups(user);
  const { data: branding } = useBrandingSettings();
  const appName = branding.applicationName || "SAS Portal v2";
  const initials = appName
    .split(" ")
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

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
          "fixed inset-y-0 left-0 z-50 flex flex-col border-r bg-card transition-all lg:static lg:z-auto",
          sidebarCollapsed ? "w-[72px]" : "w-[260px]",
          mobileSidebarOpen ? "translate-x-0" : "-translate-x-full lg:translate-x-0",
        )}
      >
        <div className="flex h-16 items-center justify-between border-b px-3">
          <div className={cn("flex min-w-0 items-center gap-2", sidebarCollapsed ? "justify-center" : "")}>
            {branding.logoUrl ? (
              <img src={branding.logoUrl} alt={appName} className="size-8 rounded-md object-contain" />
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
            const visibleItems = group.items.filter((item) => item.visible);
            if (!visibleItems.length) return null;
            const groupLabel = t(group.labelKey);

            return (
              <div key={group.labelKey} className="space-y-1">
                {!sidebarCollapsed ? (
                  <p className="px-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    {groupLabel}
                  </p>
                ) : null}
                {visibleItems.map((item) => {
                  const Icon = item.icon;
                  const isActive = location.pathname === item.to;
                  const itemTitle = t(item.titleKey);
                  const link = (
                    <Link
                      key={item.to}
                      to={item.to}
                      onClick={() => setMobileSidebarOpen(false)}
                      className={cn(
                        buttonVariants({ variant: "ghost" }),
                        "w-full gap-2 transition-colors",
                        isActive
                          ? "bg-sidebar-primary text-sidebar-primary-foreground hover:bg-sidebar-primary/90 hover:text-sidebar-primary-foreground"
                          : "text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
                        sidebarCollapsed ? "justify-center px-0" : "justify-start",
                      )}
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
                })}
              </div>
            );
          })}
        </nav>
      </aside>
    </>
  );
}
