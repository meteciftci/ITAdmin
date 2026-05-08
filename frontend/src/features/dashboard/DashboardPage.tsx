import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { useAuthStore } from "@/features/auth/auth-store";
import { useBrandingSettings } from "@/hooks/useBrandingSettings";
import { useTranslation } from "react-i18next";

export function DashboardPage() {
  const { t } = useTranslation(["dashboard"]);
  const user = useAuthStore((state) => state.user);
  const { data: branding } = useBrandingSettings();

  return (
    <section className="space-y-4">
      <div className="space-y-1">
        <h1 className="text-2xl font-semibold tracking-tight">{branding.applicationName}</h1>
        <p className="text-sm text-muted-foreground">{t("description")}</p>
      </div>
      <Card>
        <CardHeader>
          <CardTitle>{t("welcome")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <p>
            <span className="font-medium">{t("displayName")}:</span>{" "}
            {user?.displayName ?? "-"}
          </p>
          <Separator />
          <p>
            <span className="font-medium">{t("roles")}:</span>{" "}
            {user?.roles.length ? user.roles.join(", ") : "-"}
          </p>
          <p>
            <span className="font-medium">{t("permissionCount")}:</span>{" "}
            {user?.permissions.length ?? 0}
          </p>
        </CardContent>
      </Card>
    </section>
  );
}
