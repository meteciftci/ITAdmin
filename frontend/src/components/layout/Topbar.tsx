import { LogOut, Menu } from "lucide-react";
import { useNavigate } from "react-router-dom";

import { useLayoutShell } from "@/components/layout/layout-shell";
import { Button } from "@/components/ui/button";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { useAuthStore } from "@/features/auth/auth-store";
import { logout } from "@/features/auth/api";
import { useTranslation } from "react-i18next";

export function Topbar() {
  const { t } = useTranslation(["common"]);
  const navigate = useNavigate();
  const refreshToken = useAuthStore((state) => state.refreshToken);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const { setMobileSidebarOpen } = useLayoutShell();

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
      <div className="flex min-w-0 items-center gap-2">
        <Button
          variant="ghost"
          size="icon-sm"
          className="lg:hidden"
          onClick={() => setMobileSidebarOpen(true)}
          title={t("common:actions.more")}
        >
          <Menu className="size-4" />
        </Button>
      </div>
      <div className="flex items-center gap-2">
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
