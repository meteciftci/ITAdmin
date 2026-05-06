import { Moon, Sun } from "lucide-react";
import { useTranslation } from "react-i18next";

import { useTheme } from "@/components/theme/ThemeProvider";
import { Button } from "@/components/ui/button";

export function ThemeToggle() {
  const { t } = useTranslation(["common"]);
  const { theme, setTheme } = useTheme();

  const isDark = theme === "dark" || (theme === "system" && document.documentElement.classList.contains("dark"));
  const icon = isDark ? <Moon className="size-4" /> : <Sun className="size-4" />;

  return (
    <Button
      variant="outline"
      size="icon-sm"
      title={t("common:theme.toggle")}
      onClick={() => setTheme(isDark ? "light" : "dark")}
    >
      {icon}
    </Button>
  );
}
