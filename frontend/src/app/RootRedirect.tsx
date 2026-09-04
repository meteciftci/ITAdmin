import { Navigate } from "react-router-dom";

import { useAuthStore } from "@/features/auth/auth-store";

export function RootRedirect() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <Navigate to="/home" replace />;
}
