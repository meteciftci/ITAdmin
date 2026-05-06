import type { ReactNode } from "react";
import { createContext, useContext, useEffect, useMemo, useState } from "react";

export type ThemeMode = "light" | "dark" | "system";

type ThemeContextValue = {
  theme: ThemeMode;
  setTheme: (theme: ThemeMode) => void;
};

const THEME_KEY = "sasportal.theme";
const ThemeContext = createContext<ThemeContextValue | null>(null);

const getSystemTheme = (): "light" | "dark" =>
  window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";

const applyThemeClass = (theme: ThemeMode) => {
  const next = theme === "system" ? getSystemTheme() : theme;
  document.documentElement.classList.toggle("dark", next === "dark");
};

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<ThemeMode>(() => {
    const saved = localStorage.getItem(THEME_KEY);
    if (saved === "light" || saved === "dark" || saved === "system") return saved;
    return "system";
  });

  useEffect(() => {
    applyThemeClass(theme);
    localStorage.setItem(THEME_KEY, theme);
  }, [theme]);

  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => {
      if (theme === "system") applyThemeClass("system");
    };
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, [theme]);

  const value = useMemo(
    () => ({
      theme,
      setTheme: setThemeState,
    }),
    [theme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error("useTheme must be used inside ThemeProvider");
  }
  return context;
}
