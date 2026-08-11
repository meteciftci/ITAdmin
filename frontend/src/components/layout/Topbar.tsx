import { LogOut, Menu, UserRound } from "lucide-react";
import { Link, useLocation, useNavigate } from "react-router-dom";

import { getBreadcrumbKeyByPath } from "@/components/layout/breadcrumb-items";
import { useLayoutShell } from "@/components/layout/useLayoutShell";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { Button } from "@/components/ui/button";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { useAuthStore } from "@/features/auth/auth-store";
import { logout } from "@/features/auth/api";
import { useTranslation } from "react-i18next";

export function Topbar() {
  const { t } = useTranslation(["common", "navigation"]);
  const location = useLocation();
  const navigate = useNavigate();
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const user = useAuthStore((state) => state.user);
  const {
    sidebarCollapsed,
    setSidebarCollapsed,
    mobileSidebarOpen,
    setMobileSidebarOpen,
  } = useLayoutShell();
  const currentPageKey = getBreadcrumbKeyByPath(location.pathname);

  const handleLogout = async () => {
    try {
      await logout();
    } finally {
      clearAuth();
      navigate("/login", { replace: true });
    }
  };

  const handleToggleNavigation = () => {
    if (window.matchMedia("(min-width: 1024px)").matches) {
      setSidebarCollapsed(!sidebarCollapsed);
      return;
    }

    setMobileSidebarOpen(!mobileSidebarOpen);
  };

  return (
    <header className="flex h-16 shrink-0 items-center justify-between gap-3 border-b bg-card px-3 sm:px-5 lg:px-7">
      <div className="flex min-w-0 flex-1 items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={handleToggleNavigation}
          aria-label={t("navigation:navigation.toggle")}
          aria-controls="app-navigation"
          aria-expanded={
            window.matchMedia("(min-width: 1024px)").matches
              ? !sidebarCollapsed
              : mobileSidebarOpen
          }
        >
          <Menu className="size-5" />
        </Button>
        <Breadcrumb className="min-w-0">
          <BreadcrumbList>
            <BreadcrumbItem>
              {currentPageKey ? (
                <Link
                  to="/home"
                  className="max-w-40 truncate rounded-sm text-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:max-w-56"
                >
                  {t("navigation:items.home")}
                </Link>
              ) : (
                <BreadcrumbPage>{t("navigation:items.home")}</BreadcrumbPage>
              )}
            </BreadcrumbItem>
            {currentPageKey ? (
              <>
                <BreadcrumbSeparator />
                <BreadcrumbItem>
                  <BreadcrumbPage>{t(`navigation:${currentPageKey}`)}</BreadcrumbPage>
                </BreadcrumbItem>
              </>
            ) : null}
          </BreadcrumbList>
        </Breadcrumb>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <LanguageSwitcher compact />
        <ThemeToggle />
        <div className="hidden min-w-0 items-center gap-2 border-l pl-3 md:flex">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
            <UserRound className="size-4" />
          </span>
          <span className="hidden min-w-0 leading-tight lg:block">
            <span className="block max-w-40 truncate text-sm font-medium">
              {user?.displayName || user?.userName}
            </span>
            <span className="block max-w-40 truncate text-xs text-muted-foreground">
              {user?.roles.join(", ")}
            </span>
          </span>
        </div>
        <Button variant="outline" size="icon-sm" onClick={handleLogout} title={t("common:actions.logout")}>
          <LogOut className="size-4" />
          <span className="sr-only">{t("common:actions.logout")}</span>
        </Button>
      </div>
    </header>
  );
}
