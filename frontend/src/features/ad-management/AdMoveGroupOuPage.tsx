import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
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
import { AD_USER_FORM_ACTIONS_CLASSNAME } from "@/features/ad-management/ad-form-actions";
import { buildAdGroupDetailPath } from "@/features/ad-management/ad-group-detail-path";
import {
  buildAdGroupDetailReturnState,
  resolveAdGroupReturnPath,
} from "@/features/ad-management/ad-groups-return-path";
import { AD_GROUPS_LIST_PATH } from "@/features/ad-management/ad-groups-list-path";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import {
  AD_MANAGEMENT_GROUPS_QUERY_KEY,
  getAdGroupById,
  invalidateAdGroupOuMoveQueries,
  moveAdGroupOu,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getApiErrorMessage } from "@/lib/api-error";
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

export function AdMoveGroupOuPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id: groupId } = useParams<{ id: string }>();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const moduleStatus = useAdManagementModuleStatus();
  const returnPath = resolveAdGroupReturnPath(location.state, AD_GROUPS_LIST_PATH);

  const [targetOuDistinguishedName, setTargetOuDistinguishedName] = useState<string | null>(null);
  const [sameOuWarning, setSameOuWarning] = useState(false);

  const hasValidId = Boolean(groupId?.trim()) && isGuidLike(groupId);

  const groupQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_GROUPS_QUERY_KEY, "detail", groupId],
    queryFn: () => getAdGroupById(groupId!),
    enabled: hasValidId && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const currentParentOu = useMemo(
    () => getParentDistinguishedName(groupQuery.data?.distinguishedName),
    [groupQuery.data?.distinguishedName],
  );

  const moveMutation = useMutation({
    mutationFn: () =>
      moveAdGroupOu(groupId!, {
        targetOuDistinguishedName: targetOuDistinguishedName!,
      }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(response.message || t("adManagement:groups.moveOu.error"));
        return;
      }

      await invalidateAdGroupOuMoveQueries(queryClient);
      toast.success(response.message || t("adManagement:groups.moveOu.success"));
      navigate(groupId ? buildAdGroupDetailPath(groupId) : returnPath, {
        state: groupId ? buildAdGroupDetailReturnState(groupId) : undefined,
      });
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("adManagement:groups.moveOu.error")));
    },
  });

  const handleTargetOuChange = (value: string) => {
    setTargetOuDistinguishedName(value);
    const parentOu = currentParentOu?.trim();
    setSameOuWarning(Boolean(parentOu && value.trim() && parentOu.toLowerCase() === value.trim().toLowerCase()));
  };

  if (!hasValidId) {
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

  const pageTitle = groupQuery.data?.displayName
    || groupQuery.data?.samAccountName
    || t("adManagement:groups.moveOu.title");

  const canSubmit =
    Boolean(targetOuDistinguishedName?.trim())
    && !sameOuWarning
    && !moveMutation.isPending;

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-3xl space-y-4">
        <PageHeader
          title={t("adManagement:groups.moveOu.title")}
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
          {t("adManagement:groups.moveOu.description")}
        </p>

        {groupQuery.isLoading ? <LoadingState /> : null}

        {groupQuery.isError ? (
          <ErrorState
            title={t("errors:generic.title")}
            description={getApiErrorMessage(
              groupQuery.error,
              t("adManagement:groups.errors.notFound"),
            )}
          />
        ) : null}

        {groupQuery.isSuccess && groupQuery.data ? (
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
            <SectionCard title={t("adManagement:groups.moveOu.sections.groupSummary")}>
              <div className="grid gap-4 sm:grid-cols-2">
                <SummaryField
                  label={t("adManagement:groups.table.displayName")}
                  value={groupQuery.data.displayName}
                />
                <SummaryField
                  label={t("adManagement:groups.table.name")}
                  value={groupQuery.data.name}
                />
                <SummaryField
                  label={t("adManagement:groups.table.samAccountName")}
                  value={groupQuery.data.samAccountName}
                />
                <SummaryField
                  label={t("adManagement:groups.moveOu.currentOu")}
                  value={currentParentOu}
                  mono
                />
                <div className="sm:col-span-2">
                  <SummaryField
                    label={t("adManagement:groups.table.distinguishedName")}
                    value={groupQuery.data.distinguishedName}
                    mono
                  />
                </div>
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:groups.moveOu.targetOu")}>
              <p className="mb-3 text-sm text-muted-foreground">
                {t("adManagement:groups.moveOu.warnings.groupsSearchBaseScope")}
              </p>
              <AdOuSearchCombobox
                value={targetOuDistinguishedName}
                onChange={handleTargetOuChange}
                disabled={moveMutation.isPending}
                searchContext="groups"
                fieldLabelKey="adManagement:groups.moveOu.targetOu"
                placeholderKey="adManagement:groups.create.fields.ouPlaceholder"
                searchKey="adManagement:groups.create.fields.ouSearch"
                emptyKey="adManagement:groups.create.empty.ouNotFound"
                errorKey="adManagement:groups.create.errors.ouLoadFailed"
              />
              {sameOuWarning ? (
                <p className="mt-2 text-sm text-destructive">
                  {t("adManagement:groups.moveOu.sameOu")}
                </p>
              ) : null}
            </SectionCard>

            <div className={AD_USER_FORM_ACTIONS_CLASSNAME}>
              <Link
                to={returnPath}
                className={cn(buttonVariants({ variant: "outline" }))}
              >
                {t("common:actions.cancel")}
              </Link>
              <Button type="submit" disabled={!canSubmit}>
                {t("adManagement:groups.moveOu.submit")}
              </Button>
            </div>
          </form>
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
