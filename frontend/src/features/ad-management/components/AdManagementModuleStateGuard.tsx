import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { buttonVariants } from "@/components/ui/button-variants";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { cn } from "@/lib/utils";

const AD_MANAGEMENT_SETTINGS_PATH = "/settings/modules/ad-management";

type Props = {
  children: ReactNode;
};

export function AdManagementModuleStateGuard({ children }: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const moduleStatus = useAdManagementModuleStatus();

  if (moduleStatus.isLoading) {
    return <LoadingState />;
  }

  if (!moduleStatus.isConfigured) {
    return (
      <UnavailableState
        title={t("adManagement:moduleState.notConfigured.title")}
        description={t("adManagement:moduleState.notConfigured.description")}
        actionLabel={t("adManagement:moduleState.goToSettings")}
      />
    );
  }

  if (!moduleStatus.isEnabled) {
    return (
      <UnavailableState
        title={t("adManagement:moduleState.disabled.title")}
        description={t("adManagement:moduleState.disabled.description")}
        actionLabel={t("adManagement:moduleState.goToSettings")}
      />
    );
  }

  return <>{children}</>;
}

function UnavailableState({
  title,
  description,
  actionLabel,
}: {
  title: string;
  description: string;
  actionLabel: string;
}) {
  return (
    <EmptyState
      title={title}
      description={description}
      action={
        <Link
          to={AD_MANAGEMENT_SETTINGS_PATH}
          className={cn(buttonVariants({ variant: "default" }))}
        >
          {actionLabel}
        </Link>
      }
    />
  );
}
