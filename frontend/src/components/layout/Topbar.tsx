import { LogOut } from "lucide-react";
import { useNavigate } from "react-router-dom";

import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/features/auth/auth-store";
import { logout } from "@/features/auth/api";

export function Topbar() {
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const refreshToken = useAuthStore((state) => state.refreshToken);
  const clearAuth = useAuthStore((state) => state.clearAuth);

  const handleLogout = async () => {
    try {
      if (refreshToken) {
        await logout(refreshToken);
      }
    } finally {
      clearAuth();
      navigate("/login", { replace: true });
    }
  };

  return (
    <header className="flex h-14 items-center justify-between border-b bg-card px-4">
      <p className="text-sm text-muted-foreground">
        {user?.displayName ?? "Authenticated User"}
      </p>
      <Button variant="outline" size="sm" onClick={handleLogout}>
        <LogOut className="mr-2 size-4" />
        Logout
      </Button>
    </header>
  );
}
