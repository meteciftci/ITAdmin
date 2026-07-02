 
import { Suspense, type ReactNode } from "react";

import { LoadingState } from "@/components/common/LoadingState";

function RouteFallback() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <LoadingState />
    </div>
  );
}

export function LazyRoute({ children }: { children: ReactNode }) {
  return <Suspense fallback={<RouteFallback />}>{children}</Suspense>;
}
