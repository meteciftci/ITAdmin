import { useQuery } from "@tanstack/react-query";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { buttonVariants } from "@/components/ui/button-variants";
import { resolveSafeReturnPath } from "@/features/ad-management/ad-return-path";
import { AD_MANAGEMENT_USERS_QUERY_KEY, getAdUserById } from "@/features/ad-management/api";
import { AdEditUserForm } from "@/features/ad-management/components/AdEditUserForm";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

export function AdEditUserPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id: userId } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const moduleStatus = useAdManagementModuleStatus();

  const returnPath = resolveSafeReturnPath(searchParams.get("returnTo"));

  const userQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_USERS_QUERY_KEY, "detail", userId],
    queryFn: () => getAdUserById(userId!),
    enabled: Boolean(userId) && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  if (!userId) {
    return (
      <AdManagementModuleStateGuard>
        <EmptyState
          title={t("adManagement:users.errors.notFound")}
          description={t("adManagement:users.errors.notFound")}
        />
      </AdManagementModuleStateGuard>
    );
  }

  const pageTitle =
    userQuery.data?.displayName
    || userQuery.data?.samAccountName
    || t("adManagement:users.edit.pageTitle");

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={t("adManagement:users.edit.pageTitle")}
          description={pageTitle}
          actions={
            <Link
              to={returnPath}
              className={cn(buttonVariants({ variant: "outline" }))}
            >
              {t("adManagement:users.edit.actions.back")}
            </Link>
          }
        />

        {userQuery.isLoading ? <LoadingState /> : null}

        {userQuery.isError ? (
          <ErrorState
            title={t("adManagement:users.errors.detailFailed")}
            description={getApiErrorMessage(
              userQuery.error,
              t("adManagement:users.errors.detailFailed"),
            )}
          />
        ) : null}

        {userQuery.isSuccess && userQuery.data ? (
          <AdEditUserForm key={userQuery.data.id} user={userQuery.data} returnPath={returnPath} />
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
