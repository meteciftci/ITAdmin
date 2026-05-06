import { useContext } from "react";

import { LayoutShellContext } from "@/components/layout/layout-shell-context";

export function useLayoutShell() {
  const context = useContext(LayoutShellContext);
  if (!context) {
    throw new Error("useLayoutShell must be used inside LayoutShellProvider");
  }
  return context;
}
