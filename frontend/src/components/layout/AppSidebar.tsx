import { ChevronLeft, ChevronRight, LayoutDashboard, Shield, Users } from "lucide-react";
import { Link, useLocation } from "react-router-dom";

import { useLayoutShell } from "@/components/layout/layout-shell";
import { Tooltip } from "@/components/ui/tooltip";
import { buttonVariants } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
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

  const groups = [
    {
      label: t("groups.main"),
      items: [
        {
          title: t("dashboard"),
          to: "/dashboard",
          icon: LayoutDashboard,
          visible: true,
        },
      ],
    },
    {
      label: t("groups.administration"),
      items: [
        {
          title: t("users"),
          to: "/users",
          icon: Users,
          visible: canAccess(user, "Users.View"),
        },
        {
          title: t("roles"),
          to: "/roles",
          icon: Shield,
          visible: canAccess(user, "Roles.View"),
        },
      ],
    },
  ];

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
          {!sidebarCollapsed ? (
            <p className="truncate text-sm font-semibold tracking-wide text-muted-foreground">
              SAS Portal v2
            </p>
          ) : null}
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

            return (
              <div key={group.label} className="space-y-1">
                {!sidebarCollapsed ? (
                  <p className="px-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    {group.label}
                  </p>
                ) : null}
                {visibleItems.map((item) => {
                  const Icon = item.icon;
                  const isActive = location.pathname === item.to;
                  const link = (
                    <Link
                      key={item.title}
                      to={item.to}
                      onClick={() => setMobileSidebarOpen(false)}
                      className={cn(
                        buttonVariants({ variant: "ghost" }),
                        "w-full gap-2 transition-colors",
                        isActive
                          ? "bg-primary text-primary-foreground hover:bg-primary/90 hover:text-primary-foreground"
                          : "text-muted-foreground hover:bg-muted hover:text-foreground",
                        sidebarCollapsed ? "justify-center px-0" : "justify-start",
                      )}
                    >
                      <Icon className="size-4 shrink-0" />
                      {!sidebarCollapsed ? <span className="truncate">{item.title}</span> : null}
                    </Link>
                  );

                  return (
                    <Tooltip key={item.title} text={item.title} disabled={!sidebarCollapsed}>
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
