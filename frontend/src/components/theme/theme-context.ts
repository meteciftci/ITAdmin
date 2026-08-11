import { createContext } from "react";

export type ThemeMode = "light" | "dark" | "system";

export type ThemeContextValue = {
  theme: ThemeMode;
  setTheme: (theme: ThemeMode) => void;
};

export const THEME_KEY = "itadmin.theme";
export const ThemeContext = createContext<ThemeContextValue | null>(null);

const getSystemTheme = (): "light" | "dark" =>
  window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";

export const applyThemeClass = (theme: ThemeMode) => {
  const next = theme === "system" ? getSystemTheme() : theme;
  document.documentElement.classList.toggle("dark", next === "dark");
  document.documentElement.style.colorScheme = next;
};
