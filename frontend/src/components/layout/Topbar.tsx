import { LogOut, Menu } from "lucide-react";
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
  const refreshToken = useAuthStore((state) => state.refreshToken);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const { setMobileSidebarOpen } = useLayoutShell();
  const currentPageKey = getBreadcrumbKeyByPath(location.pathname);

  const handleLogout = async () => {
    try {
      if (refreshToken) {
        await logout(refreshToken);
      }
    } finally {
      clearAuth();
      navigate("/login", { replace: true });
    }
  };

  return (
    <header className="flex h-16 items-center justify-between border-b bg-card px-4 md:px-6">
      <div className="flex min-w-0 flex-1 items-center gap-2">
        <Button
          variant="ghost"
          size="icon-sm"
          className="lg:hidden"
          onClick={() => setMobileSidebarOpen(true)}
          title={t("common:actions.more")}
        >
          <Menu className="size-4" />
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
      <div className="ml-3 flex shrink-0 items-center gap-2">
        <LanguageSwitcher />
        <ThemeToggle />
        <Button variant="outline" size="sm" onClick={handleLogout}>
          <LogOut className="mr-2 size-4" />
          {t("common:actions.logout")}
        </Button>
      </div>
    </header>
  );
}
