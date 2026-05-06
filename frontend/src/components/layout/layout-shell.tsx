import type { ReactNode } from "react";
import { useMemo, useState } from "react";

import { LayoutShellContext, SIDEBAR_KEY } from "@/components/layout/layout-shell-context";

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
