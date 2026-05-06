import type { ReactNode } from "react";

import { AppSidebar } from "@/components/layout/AppSidebar";
import { Topbar } from "@/components/layout/Topbar";
import { LayoutShellProvider } from "@/components/layout/layout-shell";

type AppLayoutProps = {
  children: ReactNode;
};

export function AppLayout({ children }: AppLayoutProps) {
  return (
    <LayoutShellProvider>
      <div className="min-h-screen bg-background text-foreground lg:flex">
        <AppSidebar />
        <div className="flex min-h-screen min-w-0 flex-1 flex-col">
          <Topbar />
          <main className="min-w-0 flex-1 overflow-y-auto bg-muted/30 p-4 md:p-6">{children}</main>
        </div>
      </div>
    </LayoutShellProvider>
  );
}
