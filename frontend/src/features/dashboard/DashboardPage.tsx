import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { useAuthStore } from "@/features/auth/auth-store";

export function DashboardPage() {
  const user = useAuthStore((state) => state.user);

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold tracking-tight">SAS Portal Dashboard</h1>
      <Card>
        <CardHeader>
          <CardTitle>Welcome</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <p>
            <span className="font-medium">Display name:</span>{" "}
            {user?.displayName ?? "-"}
          </p>
          <Separator />
          <p>
            <span className="font-medium">Roles:</span>{" "}
            {user?.roles.length ? user.roles.join(", ") : "-"}
          </p>
          <p>
            <span className="font-medium">Permissions count:</span>{" "}
            {user?.permissions.length ?? 0}
          </p>
        </CardContent>
      </Card>
    </section>
  );
}
