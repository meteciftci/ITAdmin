import type { ReactNode } from "react";
import { KeyRound, ShieldCheck, UserRound } from "lucide-react";

import { PageContainer } from "@/components/common/PageContainer";
import { PageHeader } from "@/components/common/PageHeader";
import { RoleBadgeList } from "@/components/common/RoleBadgeList";
import { SectionCard } from "@/components/common/SectionCard";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { useAuthStore } from "@/features/auth/auth-store";
import { useTranslation } from "react-i18next";

function OverviewMetricCard({
  icon,
  label,
  value,
  description,
}: {
  icon: ReactNode;
  label: ReactNode;
  value: ReactNode;
  description: ReactNode;
}) {
  return (
    <Card className="min-w-0">
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0 space-y-1.5">
            <CardDescription>{label}</CardDescription>
            <CardTitle className="truncate text-2xl font-semibold tracking-tight">
              {value}
            </CardTitle>
          </div>
          <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
            {icon}
          </span>
        </div>
      </CardHeader>
      <CardContent>
        <p className="text-sm leading-6 text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  );
}

export function HomePage() {
  const { t } = useTranslation(["home"]);
  const user = useAuthStore((state) => state.user);

  return (
    <PageContainer variant="wide">
      <PageHeader title={t("title")} description={t("description")} />

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <OverviewMetricCard
          icon={<UserRound className="size-5" aria-hidden />}
          label={t("signedInAs")}
          value={user?.displayName ?? "-"}
          description={user?.userName ?? "-"}
        />
        <OverviewMetricCard
          icon={<ShieldCheck className="size-5" aria-hidden />}
          label={t("assignedRoles")}
          value={user?.roles.length ?? 0}
          description={t("assignedRolesDescription")}
        />
        <OverviewMetricCard
          icon={<KeyRound className="size-5" aria-hidden />}
          label={t("effectivePermissions")}
          value={user?.permissions.length ?? 0}
          description={t("effectivePermissionsDescription")}
        />
      </div>

      <SectionCard
        title={t("accessOverview")}
        description={t("accessOverviewDescription")}
      >
        <dl className="grid gap-5 text-sm md:grid-cols-2">
          <div className="space-y-1.5">
            <dt className="text-muted-foreground">{t("displayName")}</dt>
            <dd className="font-medium">{user?.displayName ?? "-"}</dd>
          </div>
          <div className="space-y-1.5">
            <dt className="text-muted-foreground">{t("username")}</dt>
            <dd className="font-medium">{user?.userName ?? "-"}</dd>
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <Separator />
          </div>
          <div className="space-y-2 md:col-span-2">
            <dt className="text-muted-foreground">{t("roles")}</dt>
            <dd><RoleBadgeList roles={user?.roles ?? []} /></dd>
          </div>
        </dl>
      </SectionCard>
    </PageContainer>
  );
}
