import { createContext } from "react";

export type LayoutShellContextValue = {
  sidebarCollapsed: boolean;
  setSidebarCollapsed: (next: boolean) => void;
  mobileSidebarOpen: boolean;
  setMobileSidebarOpen: (next: boolean) => void;
};

export const SIDEBAR_KEY = "itadmin.sidebar.collapsed";
export const LayoutShellContext = createContext<LayoutShellContextValue | null>(null);
