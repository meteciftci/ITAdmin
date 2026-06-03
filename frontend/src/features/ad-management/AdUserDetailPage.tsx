import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AxiosError } from "axios";

import { useAuthStore } from "@/features/auth/auth-store";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { AD_USERS_LIST_PATH } from "@/features/ad-management/ad-users-list-path";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import { AD_MANAGEMENT_USERS_QUERY_KEY, getAdUserById } from "@/features/ad-management/api";
import { AdUserAccountSummaryCards } from "@/features/ad-management/components/ad-user-detail/AdUserAccountSummaryCards";
import { AdUserBasicInfoSection } from "@/features/ad-management/components/ad-user-detail/AdUserBasicInfoSection";
import { AdUserGroupsSummarySection } from "@/features/ad-management/components/ad-user-detail/AdUserGroupsSummarySection";
import { AdUserMappedAttributesSection } from "@/features/ad-management/components/ad-user-detail/AdUserMappedAttributesSection";
import { AdUserRecentOperationsSection } from "@/features/ad-management/components/ad-user-detail/AdUserRecentOperationsSection";
import { AdUserTechnicalInfoSection } from "@/features/ad-management/components/ad-user-detail/AdUserTechnicalInfoSection";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

const editUserButtonClass = cn(
  buttonVariants({ size: "sm" }),
  "inline-flex h-8 min-h-8 items-center justify-center px-3 text-sm",
  "border border-amber-500/30 bg-amber-500/15 text-amber-700 hover:bg-amber-500/25",
  "dark:bg-amber-500/15 dark:text-amber-300 dark:hover:bg-amber-500/25",
);

const manageGroupsButtonClass = cn(
  buttonVariants({ size: "sm" }),
  "inline-flex h-8 min-h-8 items-center justify-center px-3 text-sm",
  "border border-emerald-500/30 bg-emerald-500/15 text-emerald-700 hover:bg-emerald-500/25",
  "dark:bg-emerald-500/15 dark:text-emerald-300 dark:hover:bg-emerald-500/25",
);

export function AdUserDetailPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id: userId } = useParams<{ id: string }>();
  const moduleStatus = useAdManagementModuleStatus();
  const currentUser = useAuthStore((state) => state.user);
  const canUpdateUser = canAccess(currentUser, "AdManagement.Users.Update");
  const canManageGroups = canAccess(currentUser, "AdManagement.Users.Groups.View");
  const canViewOperationLogs = canAccess(currentUser, "AdOperationLogs.View");
  const [showEmptyMappedFields, setShowEmptyMappedFields] = useState(false);

  const hasValidId = Boolean(userId?.trim()) && isGuidLike(userId);

  const userQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_USERS_QUERY_KEY, "detail", userId],
    queryFn: () => getAdUserById(userId!),
    enabled: hasValidId && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  if (!hasValidId) {
    return (
      <AdManagementModuleStateGuard>
        <div className="mx-auto w-full max-w-7xl space-y-4">
          <EmptyState
            title={t("adManagement:users.errors.notFound")}
            description={t("adManagement:users.errors.notFound")}
          />
        </div>
      </AdManagementModuleStateGuard>
    );
  }

  const user = userQuery.data;
  const pageTitle = user?.displayName || user?.samAccountName || t("adManagement:users.detail.page.title");
  const pageDescription =
    user?.userPrincipalName
    || user?.distinguishedName
    || user?.samAccountName
    || undefined;

  const isNotFound =
    userQuery.isError
    && userQuery.error instanceof AxiosError
    && userQuery.error.response?.status === 404;

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={pageTitle}
          description={pageDescription}
          actions={
            <div className="flex flex-wrap items-center gap-2">
              <Link
                to={AD_USERS_LIST_PATH}
                className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
              >
                {t("adManagement:users.detail.page.back")}
              </Link>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => userQuery.refetch()}
                disabled={userQuery.isFetching}
              >
                {t("adManagement:users.actions.refresh")}
              </Button>
              {canUpdateUser && user ? (
                <Link
                  to={`/ad-management/users/${user.id}/edit`}
                  className={editUserButtonClass}
                >
                  {t("adManagement:users.actions.edit")}
                </Link>
              ) : null}
              {canManageGroups && user ? (
                <Link
                  to={`/ad-management/users/${user.id}/groups`}
                  className={manageGroupsButtonClass}
                >
                  {t("adManagement:users.actions.manageGroups")}
                </Link>
              ) : null}
            </div>
          }
        />

        {userQuery.isLoading ? <LoadingState /> : null}

        {userQuery.isError && !isNotFound ? (
          <ErrorState
            title={t("adManagement:users.errors.detailFailed")}
            description={getApiErrorMessage(
              userQuery.error,
              t("adManagement:users.errors.detailFailed"),
            )}
          />
        ) : null}

        {isNotFound ? (
          <EmptyState
            title={t("adManagement:users.errors.notFound")}
            description={t("adManagement:users.errors.notFound")}
          />
        ) : null}

        {userQuery.isSuccess && user ? (
          <div className="space-y-4">
            <div className="space-y-2">
              <h2 className="text-sm font-medium">{t("adManagement:users.detail.page.accountSummary")}</h2>
              <AdUserAccountSummaryCards user={user} />
            </div>
            <AdUserBasicInfoSection user={user} />
            <AdUserTechnicalInfoSection user={user} />
            <AdUserMappedAttributesSection
              user={user}
              showEmptyFields={showEmptyMappedFields}
              onShowEmptyFieldsChange={setShowEmptyMappedFields}
            />
            <AdUserGroupsSummarySection user={user} canManageGroups={canManageGroups} />
            {canViewOperationLogs ? (
              <AdUserRecentOperationsSection userId={user.id} enabled />
            ) : null}
          </div>
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
