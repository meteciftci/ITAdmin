import type { ReactNode } from "react";
import { createContext, useContext, useMemo, useState } from "react";

type LayoutShellContextValue = {
  sidebarCollapsed: boolean;
  setSidebarCollapsed: (next: boolean) => void;
  mobileSidebarOpen: boolean;
  setMobileSidebarOpen: (next: boolean) => void;
};

const SIDEBAR_KEY = "sasportal.sidebar.collapsed";
const LayoutShellContext = createContext<LayoutShellContextValue | null>(null);

export function LayoutShellProvider({ children }: { children: ReactNode }) {
  const [sidebarCollapsed, setSidebarCollapsedState] = useState<boolean>(() => {
    return localStorage.getItem(SIDEBAR_KEY) === "true";
  });
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);

  const setSidebarCollapsed = (next: boolean) => {
    setSidebarCollapsedState(next);
    localStorage.setItem(SIDEBAR_KEY, String(next));
  };

  const value = useMemo(
    () => ({
      sidebarCollapsed,
      setSidebarCollapsed,
      mobileSidebarOpen,
      setMobileSidebarOpen,
    }),
    [mobileSidebarOpen, sidebarCollapsed],
  );

  return <LayoutShellContext.Provider value={value}>{children}</LayoutShellContext.Provider>;
}

export function useLayoutShell() {
  const context = useContext(LayoutShellContext);
  if (!context) {
    throw new Error("useLayoutShell must be used inside LayoutShellProvider");
  }
  return context;
}
