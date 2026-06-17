import { useQuery } from "@tanstack/react-query";
import { Link, useLocation, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { buttonVariants } from "@/components/ui/button-variants";
import { getAdGroupPrimaryLabel } from "@/features/ad-management/ad-group-display-labels";
import { resolveAdGroupReturnPath } from "@/features/ad-management/ad-groups-return-path";
import { AD_GROUPS_LIST_PATH } from "@/features/ad-management/ad-groups-list-path";
import { AD_MANAGEMENT_GROUPS_QUERY_KEY, getAdGroupById } from "@/features/ad-management/api";
import { AdEditGroupForm } from "@/features/ad-management/components/AdEditGroupForm";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";
import { cn } from "@/lib/utils";

export function AdEditGroupPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id: groupId } = useParams<{ id: string }>();
  const location = useLocation();
  const moduleStatus = useAdManagementModuleStatus();
  const returnPath = resolveAdGroupReturnPath(location.state, AD_GROUPS_LIST_PATH);

  const groupQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_GROUPS_QUERY_KEY, "detail", groupId],
    queryFn: () => getAdGroupById(groupId!),
    enabled: Boolean(groupId) && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  if (!groupId) {
    return (
      <AdManagementModuleStateGuard>
        <div className="mx-auto w-full max-w-7xl space-y-4">
          <EmptyState
            title={t("adManagement:groups.errors.notFound")}
            description={t("adManagement:groups.errors.notFound")}
          />
        </div>
      </AdManagementModuleStateGuard>
    );
  }

  const pageTitle =
    groupQuery.data
      ? getAdGroupPrimaryLabel(groupQuery.data)
      : t("adManagement:groups.edit.pageTitle");

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={t("adManagement:groups.edit.pageTitle")}
          description={pageTitle}
          actions={
            <Link
              to={returnPath}
              className={cn(buttonVariants({ variant: "outline" }))}
            >
              {t("common:actions.back")}
            </Link>
          }
        />

        {groupQuery.isLoading ? <LoadingState /> : null}

        {groupQuery.isError ? (
          <ErrorState
            title={t("adManagement:groups.errors.notFound")}
            description={getAdManagementApiErrorMessage(
              groupQuery.error,
              t,
              "adManagement:groups.errors.notFound",
            )}
          />
        ) : null}

        {groupQuery.data ? (
          <AdEditGroupForm group={groupQuery.data} returnPath={returnPath} />
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
