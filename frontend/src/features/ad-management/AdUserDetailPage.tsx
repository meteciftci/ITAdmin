import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AxiosError } from "axios";

import { useAuthStore } from "@/features/auth/auth-store";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import { AD_MANAGEMENT_USERS_QUERY_KEY, getAdUserById } from "@/features/ad-management/api";
import { AdUserAccountSummaryCards } from "@/features/ad-management/components/ad-user-detail/AdUserAccountSummaryCards";
import { AdUserAccountExpirationSection } from "@/features/ad-management/components/ad-user-detail/AdUserAccountExpirationSection";
import { AdUserBasicInfoSection } from "@/features/ad-management/components/ad-user-detail/AdUserBasicInfoSection";
import { AdUserDetailHeaderActions } from "@/features/ad-management/components/ad-user-detail/AdUserDetailHeaderActions";
import { AdUserEffectiveGroupsSection } from "@/features/ad-management/components/ad-user-detail/AdUserEffectiveGroupsSection";
import { AdUserManagerSection } from "@/features/ad-management/components/ad-user-detail/AdUserManagerSection";
import { AdUserMappedAttributesSection } from "@/features/ad-management/components/ad-user-detail/AdUserMappedAttributesSection";
import { AdUserRecentOperationsSection } from "@/features/ad-management/components/ad-user-detail/AdUserRecentOperationsSection";
import { AdUserTechnicalInfoSection } from "@/features/ad-management/components/ad-user-detail/AdUserTechnicalInfoSection";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";

export function AdUserDetailPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id: userId } = useParams<{ id: string }>();
  const moduleStatus = useAdManagementModuleStatus();
  const currentUser = useAuthStore((state) => state.user);
  const canUpdateUser = canAccess(currentUser, PermissionCodes.AdManagement.Users.Update);
  const canManageGroups = canAccess(currentUser, PermissionCodes.AdManagement.Users.Groups.View);
  const canMoveOu = canAccess(currentUser, PermissionCodes.AdManagement.Users.MoveOu);
  const canEnableUser = canAccess(currentUser, PermissionCodes.AdManagement.Users.Enable);
  const canDisableUser = canAccess(currentUser, PermissionCodes.AdManagement.Users.Disable);
  const canUnlockUser = canAccess(currentUser, PermissionCodes.AdManagement.Users.Unlock);
  const canViewOperationLogs = canAccess(currentUser, PermissionCodes.AdOperationLogs.View);
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
            user ? (
              <AdUserDetailHeaderActions
                user={user}
                isFetching={userQuery.isFetching}
                onRefresh={() => userQuery.refetch()}
                canUpdateUser={canUpdateUser}
                canManageGroups={canManageGroups}
                canMoveOu={canMoveOu}
                canEnableUser={canEnableUser}
                canDisableUser={canDisableUser}
                canUnlockUser={canUnlockUser}
              />
            ) : null
          }
        />

        {userQuery.isLoading ? <LoadingState /> : null}

        {userQuery.isError && !isNotFound ? (
          <ErrorState
            title={t("adManagement:users.errors.detailFailed")}
            description={getAdManagementApiErrorMessage(
              userQuery.error,
              t,
              "adManagement:users.errors.detailFailed",
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
            <div className="grid gap-4 xl:grid-cols-2">
              <AdUserManagerSection user={user} canUpdate={canUpdateUser} />
              <AdUserAccountExpirationSection user={user} canUpdate={canUpdateUser} />
            </div>
            <AdUserTechnicalInfoSection user={user} />
            <AdUserMappedAttributesSection
              user={user}
              showEmptyFields={showEmptyMappedFields}
              onShowEmptyFieldsChange={setShowEmptyMappedFields}
            />
            {canManageGroups ? (
              <AdUserEffectiveGroupsSection userId={user.id} />
            ) : null}
            {canViewOperationLogs ? (
              <AdUserRecentOperationsSection userId={user.id} enabled />
            ) : null}
          </div>
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
