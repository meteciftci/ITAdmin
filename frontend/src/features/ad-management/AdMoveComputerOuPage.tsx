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
import { isAdComputerAccountOperationRestricted } from "@/features/ad-management/ad-computer-account-guard";
import { getAdComputerPrimaryLabel } from "@/features/ad-management/ad-computer-display-labels";
import { buildAdComputerDetailPath } from "@/features/ad-management/ad-computer-detail-path";
import { getParentDistinguishedName } from "@/features/ad-management/ad-ldap-dn";
import {
  buildAdComputerDetailReturnState,
  resolveAdComputerReturnPath,
} from "@/features/ad-management/ad-computers-return-path";
import { AD_COMPUTERS_LIST_PATH } from "@/features/ad-management/ad-computers-list-path";
import { AD_USER_FORM_ACTIONS_CLASSNAME } from "@/features/ad-management/ad-form-actions";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import {
  AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
  getAdComputerById,
  invalidateAdManagementComputerQueries,
  moveAdComputerOu,
} from "@/features/ad-management/api";
import { AdComputerMoveOuForm } from "@/features/ad-management/components/AdComputerMoveOuForm";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
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

export function AdMoveComputerOuPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id: computerId } = useParams<{ id: string }>();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const moduleStatus = useAdManagementModuleStatus();
  const returnPath = resolveAdComputerReturnPath(location.state, AD_COMPUTERS_LIST_PATH);

  const [targetOuDistinguishedName, setTargetOuDistinguishedName] = useState<string | null>(null);
  const [sameOuWarning, setSameOuWarning] = useState(false);

  const hasValidId = Boolean(computerId?.trim()) && isGuidLike(computerId);

  const computerQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_COMPUTERS_QUERY_KEY, "detail", computerId],
    queryFn: () => getAdComputerById(computerId!),
    enabled: hasValidId && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const currentParentOu = useMemo(() => {
    if (!computerQuery.data) {
      return null;
    }

    return computerQuery.data.parentOuDistinguishedName
      ?? getParentDistinguishedName(computerQuery.data.distinguishedName);
  }, [computerQuery.data]);

  const isProtected = computerQuery.data
    ? isAdComputerAccountOperationRestricted(computerQuery.data)
    : false;

  const moveMutation = useMutation({
    mutationFn: () =>
      moveAdComputerOu(computerId!, {
        targetOuDistinguishedName: targetOuDistinguishedName!,
      }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(
          response.message || t("adManagement:computers.moveOu.messages.moveFailed"),
        );
        return;
      }

      await invalidateAdManagementComputerQueries(queryClient);
      const fallbackMessage = response.message?.includes("zaten")
        || response.message?.toLowerCase().includes("already")
        ? t("adManagement:computers.moveOu.messages.alreadyInOu")
        : t("adManagement:computers.moveOu.messages.moved");
      toast.success(response.message || fallbackMessage);
      navigate(computerId ? buildAdComputerDetailPath(computerId) : returnPath, {
        state: computerId ? buildAdComputerDetailReturnState(computerId) : undefined,
      });
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:computers.moveOu.messages.moveFailed")),
      );
    },
  });

  const handleTargetOuChange = (value: string) => {
    setTargetOuDistinguishedName(value);
    const parentOu = currentParentOu?.trim();
    setSameOuWarning(Boolean(
      parentOu && value.trim() && parentOu.toLowerCase() === value.trim().toLowerCase(),
    ));
  };

  if (!hasValidId) {
    return (
      <AdManagementModuleStateGuard>
        <div className="mx-auto w-full max-w-7xl space-y-4">
          <EmptyState
            title={t("adManagement:computers.errors.notFound")}
            description={t("adManagement:computers.errors.notFound")}
          />
        </div>
      </AdManagementModuleStateGuard>
    );
  }

  const pageTitle = computerQuery.data
    ? getAdComputerPrimaryLabel(computerQuery.data)
    : t("adManagement:computers.moveOu.pageTitle");

  const canSubmit =
    Boolean(targetOuDistinguishedName?.trim())
    && !sameOuWarning
    && !isProtected
    && !moveMutation.isPending;

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-3xl space-y-4">
        <PageHeader
          title={t("adManagement:computers.moveOu.pageTitle")}
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
          {t("adManagement:computers.moveOu.pageDescription")}
        </p>

        {computerQuery.isLoading ? <LoadingState /> : null}

        {computerQuery.isError ? (
          <ErrorState
            title={t("adManagement:computers.errors.detailFailed")}
            description={getApiErrorMessage(
              computerQuery.error,
              t("adManagement:computers.errors.detailFailed"),
            )}
          />
        ) : null}

        {computerQuery.isSuccess && computerQuery.data ? (
          isProtected ? (
            <EmptyState
              title={t("adManagement:computers.moveOu.protectedTitle")}
              description={t("adManagement:computers.moveOu.protected")}
            />
          ) : (
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
              <SectionCard title={t("adManagement:computers.moveOu.sections.computerSummary")}>
                <div className="grid gap-4 sm:grid-cols-2">
                  <SummaryField
                    label={t("adManagement:computers.moveOu.fields.name")}
                    value={computerQuery.data.name}
                  />
                  <SummaryField
                    label={t("adManagement:computers.moveOu.fields.samAccountName")}
                    value={computerQuery.data.samAccountName}
                  />
                  <SummaryField
                    label={t("adManagement:computers.moveOu.fields.dnsHostName")}
                    value={computerQuery.data.dnsHostName}
                  />
                  <SummaryField
                    label={t("adManagement:computers.moveOu.currentOu")}
                    value={currentParentOu}
                    mono
                  />
                  <div className="sm:col-span-2">
                    <SummaryField
                      label={t("adManagement:computers.moveOu.fields.currentDn")}
                      value={computerQuery.data.distinguishedName}
                      mono
                    />
                  </div>
                </div>
              </SectionCard>

              <SectionCard title={t("adManagement:computers.moveOu.sections.targetOu")}>
                <AdComputerMoveOuForm
                  targetOuDistinguishedName={targetOuDistinguishedName}
                  onTargetOuChange={handleTargetOuChange}
                  disabled={moveMutation.isPending}
                  sameOuWarning={sameOuWarning}
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
                  {t("adManagement:computers.moveOu.actions.submit")}
                </Button>
              </div>
            </form>
          )
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
