import { Check, Monitor, Moon, Sun } from "lucide-react";
import { useTranslation } from "react-i18next";

import { useTheme } from "@/components/theme/useTheme";
import { Button } from "@/components/ui/button";
import {
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRoot,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import type { ThemeMode } from "@/components/theme/theme-context";

export function ThemeToggle() {
  const { t } = useTranslation(["common"]);
  const { theme, setTheme } = useTheme();

  const isDark = theme === "dark" || (theme === "system" && document.documentElement.classList.contains("dark"));
  const icon = isDark ? <Moon className="size-4" /> : <Sun className="size-4" />;
  const options: Array<{ value: ThemeMode; icon: typeof Sun }> = [
    { value: "light", icon: Sun },
    { value: "dark", icon: Moon },
    { value: "system", icon: Monitor },
  ];

  return (
    <DropdownMenuRoot
      trigger={
        <Button
          variant="outline"
          size="icon-sm"
          title={t("common:theme.toggle")}
          aria-label={t("common:theme.toggle")}
        >
          {icon}
        </Button>
      }
      contentProps={{ align: "end" }}
      content={
        <>
          <DropdownMenuLabel>{t("common:theme.label")}</DropdownMenuLabel>
          <DropdownMenuSeparator />
          {options.map((option) => {
            const Icon = option.icon;
            return (
              <DropdownMenuItem
                key={option.value}
                onClick={() => setTheme(option.value)}
                aria-pressed={theme === option.value}
                className="gap-2"
              >
                <Icon className="size-4 text-muted-foreground" />
                <span className="flex-1">{t(`common:theme.${option.value}`)}</span>
                {theme === option.value ? <Check className="size-4 text-primary" /> : null}
              </DropdownMenuItem>
            );
          })}
        </>
      }
    />
  );
}
