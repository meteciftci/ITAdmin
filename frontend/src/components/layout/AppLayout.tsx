import type { ReactNode } from "react";

import { useQueryClient } from "@tanstack/react-query";

import { ServiceUnavailableState } from "@/components/common/ServiceUnavailableState";
import { AppSidebar } from "@/components/layout/AppSidebar";
import { Topbar } from "@/components/layout/Topbar";
import { LayoutShellProvider } from "@/components/layout/layout-shell";
import { useAuthStore } from "@/features/auth/auth-store";
import { useReadinessStatus } from "@/hooks/useReadinessStatus";

type AppLayoutProps = {
  children: ReactNode;
};

export function AppLayout({ children }: AppLayoutProps) {
  const accessToken = useAuthStore((state) => state.accessToken);
  const queryClient = useQueryClient();
  const readiness = useReadinessStatus({ enabled: Boolean(accessToken) });

  const showServiceUnavailable =
    Boolean(accessToken) &&
    Boolean(readiness.data) &&
    !readiness.isHealthy;

  return (
    <LayoutShellProvider>
      <div className="min-h-screen bg-background text-foreground lg:flex">
        <AppSidebar />
        <div className="flex min-h-screen min-w-0 flex-1 flex-col">
          <Topbar />
          <main className="min-w-0 flex-1 overflow-y-auto bg-muted/30 p-4 md:p-6">
            {showServiceUnavailable && readiness.data ? (
              <ServiceUnavailableState
                readiness={readiness.data}
                isLoading={readiness.isFetching}
                onRetry={() => {
                  void queryClient.invalidateQueries({ queryKey: ["health", "readiness"] });
                }}
              />
            ) : (
              children
            )}
          </main>
        </div>
      </div>
    </LayoutShellProvider>
  );
}
