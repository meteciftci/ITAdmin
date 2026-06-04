import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { getParentDistinguishedName } from "@/features/ad-management/ad-ldap-dn";
import { resolveAdUserReturnPathFromLocation } from "@/features/ad-management/ad-return-path";
import { AD_USERS_LIST_PATH } from "@/features/ad-management/ad-users-list-path";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import {
  AD_MANAGEMENT_USERS_QUERY_KEY,
  getAdUserById,
  invalidateAdManagementUserQueries,
  moveAdUserOu,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getApiErrorMessage } from "@/lib/api-error";
import { AD_USER_FORM_ACTIONS_CLASSNAME } from "@/features/ad-management/ad-form-actions";
import { cn } from "@/lib/utils";

function SummaryField({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: string | null | undefined;
  mono?: boolean;
}) {
  const display = value?.trim() || "-";

  return (
    <div className="space-y-1">
      <div className="text-xs font-medium text-muted-foreground">{label}</div>
      <div
        className={cn("text-sm", mono && "font-mono text-xs text-muted-foreground break-all")}
        title={display}
      >
        {display}
      </div>
    </div>
  );
}

export function AdMoveUserOuPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id: userId } = useParams<{ id: string }>();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const moduleStatus = useAdManagementModuleStatus();
  const returnPath = resolveAdUserReturnPathFromLocation(
    location.state,
    searchParams,
    AD_USERS_LIST_PATH,
  );

  const [targetOuDistinguishedName, setTargetOuDistinguishedName] = useState<string | null>(null);

  const hasValidId = Boolean(userId?.trim()) && isGuidLike(userId);

  const userQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_USERS_QUERY_KEY, "detail", userId],
    queryFn: () => getAdUserById(userId!),
    enabled: hasValidId && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const currentParentOu = useMemo(
    () => getParentDistinguishedName(userQuery.data?.distinguishedName),
    [userQuery.data?.distinguishedName],
  );

  const moveMutation = useMutation({
    mutationFn: () =>
      moveAdUserOu(userId!, {
        targetOuDistinguishedName: targetOuDistinguishedName!,
      }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(t("adManagement:users.moveOu.messages.moveFailed"));
        return;
      }

      await invalidateAdManagementUserQueries(queryClient);
      toast.success(
        response.message
        || (response.previousDistinguishedName === response.distinguishedName
          ? t("adManagement:users.moveOu.messages.alreadyInOu")
          : t("adManagement:users.moveOu.messages.moved")),
      );
      navigate(returnPath);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:users.moveOu.messages.moveFailed")),
      );
    },
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

  const pageTitle = userQuery.data?.displayName
    || userQuery.data?.samAccountName
    || t("adManagement:users.moveOu.pageTitle");

  const canSubmit = Boolean(targetOuDistinguishedName?.trim()) && !moveMutation.isPending;

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-3xl space-y-4">
        <PageHeader
          title={t("adManagement:users.moveOu.pageTitle")}
          description={pageTitle}
          actions={
            <Link
              to={returnPath}
              className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
            >
              {t("common:actions.back")}
            </Link>
          }
        />

        <p className="text-sm text-muted-foreground">
          {t("adManagement:users.moveOu.pageDescription")}
        </p>

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
          <form
            className="space-y-4"
            onSubmit={(event) => {
              event.preventDefault();
              if (!canSubmit) {
                return;
              }
              moveMutation.mutate();
            }}
          >
            <SectionCard title={t("adManagement:users.moveOu.sections.userSummary")}>
              <div className="grid gap-4 sm:grid-cols-2">
                <SummaryField
                  label={t("adManagement:users.moveOu.fields.displayName")}
                  value={userQuery.data.displayName}
                />
                <SummaryField
                  label={t("adManagement:users.moveOu.fields.samAccountName")}
                  value={userQuery.data.samAccountName}
                />
                <SummaryField
                  label={t("adManagement:users.moveOu.fields.userPrincipalName")}
                  value={userQuery.data.userPrincipalName}
                />
                <SummaryField
                  label={t("adManagement:users.moveOu.fields.currentOu")}
                  value={currentParentOu}
                  mono
                />
                <div className="sm:col-span-2">
                  <SummaryField
                    label={t("adManagement:users.moveOu.fields.currentDn")}
                    value={userQuery.data.distinguishedName}
                    mono
                  />
                </div>
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:users.moveOu.sections.targetOu")}>
              <p className="mb-3 text-sm text-muted-foreground">
                {t("adManagement:users.moveOu.warnings.usersRootScope")}
              </p>
              <AdOuSearchCombobox
                value={targetOuDistinguishedName}
                onChange={setTargetOuDistinguishedName}
                disabled={moveMutation.isPending}
                fieldLabelKey="adManagement:users.moveOu.fields.targetOu"
                placeholderKey="adManagement:users.moveOu.fields.targetOuPlaceholder"
                searchKey="adManagement:users.moveOu.fields.targetOuSearch"
                emptyKey="adManagement:users.moveOu.empty.ouNotFound"
                errorKey="adManagement:users.moveOu.errors.ouLoadFailed"
              />
            </SectionCard>

            <div className={AD_USER_FORM_ACTIONS_CLASSNAME}>
              <Link
                to={returnPath}
                className={cn(buttonVariants({ variant: "outline" }))}
              >
                {t("common:actions.cancel")}
              </Link>
              <Button type="submit" disabled={!canSubmit}>
                {t("adManagement:users.moveOu.actions.submit")}
              </Button>
            </div>
          </form>
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
