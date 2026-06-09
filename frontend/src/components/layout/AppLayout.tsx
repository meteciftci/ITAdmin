import type { ReactNode } from "react";

import { AppFooter } from "@/components/layout/AppFooter";
import { AppSidebar } from "@/components/layout/AppSidebar";
import { Topbar } from "@/components/layout/Topbar";
import { LayoutShellProvider } from "@/components/layout/layout-shell";
import { IdleSessionManager } from "@/features/auth/components/IdleSessionManager";

type AppLayoutProps = {
  children: ReactNode;
};

export function AppLayout({ children }: AppLayoutProps) {
  return (
    <LayoutShellProvider>
      <div className="h-screen overflow-hidden bg-background text-foreground lg:flex">
        <AppSidebar />
        <div className="flex h-screen min-w-0 flex-1 flex-col overflow-hidden">
          <Topbar />
          <main className="min-h-0 min-w-0 flex-1 overflow-y-auto bg-muted/30 p-4 md:p-6">
            {children}
          </main>
          <AppFooter />
        </div>
      </div>
      <IdleSessionManager />
    </LayoutShellProvider>
  );
}
