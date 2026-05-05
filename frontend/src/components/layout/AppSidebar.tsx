import { LayoutDashboard, LockKeyhole, Shield, Users } from "lucide-react";
import { Link, useLocation } from "react-router-dom";

import { buttonVariants } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

export function AppSidebar() {
  const location = useLocation();
  const user = useAuthStore((state) => state.user);

  const menu = [
    {
      title: "Dashboard",
      to: "/dashboard",
      icon: LayoutDashboard,
      visible: true,
      enabled: true,
    },
    {
      title: "Users",
      to: "/users",
      icon: Users,
      visible: canAccess(user, "Users.View"),
      enabled: true,
    },
    {
      title: "Roles",
      to: "/roles",
      icon: Shield,
      visible: canAccess(user, "Roles.View"),
      enabled: true,
    },
    {
      title: "Permissions",
      to: "#",
      icon: LockKeyhole,
      visible: canAccess(user, "Permissions.View"),
      enabled: false,
    },
  ];

  return (
    <aside className="w-full border-r bg-card lg:w-64">
      <div className="p-4">
        <p className="text-sm font-semibold tracking-wide text-muted-foreground">
          SAS Portal v2
        </p>
      </div>
      <Separator />
      <nav className="space-y-1 p-3">
        {menu
          .filter((item) => item.visible)
          .map((item) => {
            const Icon = item.icon;
            const isActive = item.to !== "#" && location.pathname === item.to;

            return (
              item.enabled ? (
                <Link
                  key={item.title}
                  to={item.to}
                  className={cn(
                    buttonVariants({ variant: isActive ? "default" : "ghost" }),
                    "w-full justify-start gap-2",
                  )}
                >
                  <Icon className="size-4" />
                  {item.title}
                </Link>
              ) : (
                <button
                  key={item.title}
                  type="button"
                  disabled
                  className={cn(
                    buttonVariants({ variant: "ghost" }),
                    "w-full justify-start gap-2",
                  )}
                >
                  <Icon className="size-4" />
                  {item.title}
                </button>
              )
            );
          })}
      </nav>
    </aside>
  );
}
