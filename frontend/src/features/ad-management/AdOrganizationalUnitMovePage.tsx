import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AxiosError } from "axios";
import { toast } from "sonner";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";
import { isInvalidOrganizationalUnitMoveTarget } from "@/features/ad-management/ad-ldap-dn";
import {
  buildAdOrganizationalUnitDetailPath,
} from "@/features/ad-management/ad-ou-detail-path";
import {
  getAdOrganizationalUnitParentPath,
  getAdOrganizationalUnitPrimaryLabel,
} from "@/features/ad-management/ad-ou-display-labels";
import { resolveAdOrganizationalUnitsReturnPath } from "@/features/ad-management/ad-ous-return-path";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import {
  AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY,
  getAdOrganizationalUnitById,
  invalidateAdOrganizationalUnitQueries,
  moveAdOrganizationalUnit,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdOrganizationalUnitTechnicalField } from "@/features/ad-management/components/AdOrganizationalUnitTechnicalField";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type { AdOrganizationalUnitDetail } from "@/features/ad-management/types";
import { cn } from "@/lib/utils";

type MoveFormProps = {
  organizationalUnit: AdOrganizationalUnitDetail;
  returnPath: string;
  locationState: unknown;
};

function AdOrganizationalUnitMoveForm({
  organizationalUnit,
  returnPath,
  locationState,
}: MoveFormProps) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const moduleStatus = useAdManagementModuleStatus();
  const detailPath = buildAdOrganizationalUnitDetailPath(organizationalUnit.objectGuid);

  const [targetParentDistinguishedName, setTargetParentDistinguishedName] = useState<string | null>(null);

  const isInvalidTarget = targetParentDistinguishedName
    ? isInvalidOrganizationalUnitMoveTarget(
      organizationalUnit.distinguishedName,
      targetParentDistinguishedName,
    )
    : false;

  const moveMutation = useMutation({
    mutationFn: () =>
      moveAdOrganizationalUnit(organizationalUnit.objectGuid, {
        targetParentDistinguishedName: targetParentDistinguishedName!.trim(),
      }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(
          resolveAdManagementApiMessage(
            t,
            response,
            "adManagement:organizationalUnits.move.messages.moveFailed",
          ),
        );
        return;
      }

      await invalidateAdOrganizationalUnitQueries(queryClient);
      toast.success(t("adManagement:organizationalUnits.move.messages.moved"));
      navigate(detailPath, { state: locationState });
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:organizationalUnits.move.messages.moveFailed",
        ),
      );
    },
  });

  const canSubmit =
    moduleStatus.isOperational
    && Boolean(targetParentDistinguishedName?.trim())
    && !isInvalidTarget
    && !moveMutation.isPending;

  return (
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
      <SectionCard title={t("adManagement:organizationalUnits.move.sections.sourceOu")}>
        <div className="grid gap-4 md:grid-cols-2">
          <AdOrganizationalUnitTechnicalField
            label={t("adManagement:organizationalUnits.fields.name")}
            value={getAdOrganizationalUnitPrimaryLabel(organizationalUnit)}
            monospace={false}
          />
          <AdOrganizationalUnitTechnicalField
            label={t("adManagement:organizationalUnits.fields.parentPath")}
            value={
              getAdOrganizationalUnitParentPath(organizationalUnit.canonicalName)
              || organizationalUnit.parentDistinguishedName
            }
            fullWidth
          />
          <AdOrganizationalUnitTechnicalField
            label={t("adManagement:organizationalUnits.fields.canonicalName")}
            value={organizationalUnit.canonicalName}
            fullWidth
          />
          <AdOrganizationalUnitTechnicalField
            label={t("adManagement:organizationalUnits.fields.distinguishedName")}
            value={organizationalUnit.distinguishedName}
            fullWidth
          />
        </div>
      </SectionCard>

      <SectionCard title={t("adManagement:organizationalUnits.move.formTitle")}>
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {t("adManagement:organizationalUnits.move.description")}
          </p>

          <AdOuSearchCombobox
            value={targetParentDistinguishedName}
            onChange={setTargetParentDistinguishedName}
            disabled={moveMutation.isPending}
            searchContext="manage"
            fieldLabelKey="adManagement:organizationalUnits.fields.targetParent"
            placeholderKey="adManagement:organizationalUnits.fields.targetParentPlaceholder"
            searchKey="adManagement:organizationalUnits.fields.parentSearch"
            emptyKey="adManagement:organizationalUnits.empty.notFound"
            errorKey="adManagement:organizationalUnits.errors.loadFailed"
            excludeDistinguishedName={organizationalUnit.distinguishedName}
          />

          {isInvalidTarget ? (
            <p className="text-sm text-destructive">
              {t("adManagement:organizationalUnits.move.invalidTarget")}
            </p>
          ) : null}
        </div>
      </SectionCard>

      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" disabled={!canSubmit}>
          {t("adManagement:organizationalUnits.move.actions.submit")}
        </Button>
        <Link to={returnPath} className={cn(buttonVariants({ variant: "outline" }))}>
          {t("common:actions.cancel")}
        </Link>
      </div>
    </form>
  );
}

export function AdOrganizationalUnitMovePage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id } = useParams();
  const location = useLocation();
  const moduleStatus = useAdManagementModuleStatus();

  const organizationalUnitId = id?.trim() ?? "";
  const hasValidId = isGuidLike(organizationalUnitId);
  const detailPath = buildAdOrganizationalUnitDetailPath(organizationalUnitId);
  const returnPath = resolveAdOrganizationalUnitsReturnPath(location.state, detailPath);

  const detailQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY, "detail", organizationalUnitId],
    queryFn: () => getAdOrganizationalUnitById(organizationalUnitId),
    enabled: moduleStatus.isOperational && hasValidId,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const organizationalUnit = detailQuery.data;
  const pageDescription = organizationalUnit
    ? getAdOrganizationalUnitPrimaryLabel(organizationalUnit)
    : undefined;

  const isNotFound =
    detailQuery.isError
    && detailQuery.error instanceof AxiosError
    && detailQuery.error.response?.status === 404;

  if (!hasValidId) {
    return (
      <AdManagementModuleStateGuard>
        <EmptyState title={t("adManagement:organizationalUnits.detail.invalidId")} />
      </AdManagementModuleStateGuard>
    );
  }

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={t("adManagement:organizationalUnits.move.pageTitle")}
          description={pageDescription}
          actions={
            <Link to={returnPath} className={cn(buttonVariants({ variant: "outline" }))}>
              {t("common:actions.back")}
            </Link>
          }
        />

        {detailQuery.isLoading ? <LoadingState /> : null}

        {detailQuery.isError && !isNotFound ? (
          <ErrorState
            title={t("errors:generic.title")}
            description={getAdManagementApiErrorMessage(
              detailQuery.error,
              t,
              "errors:generic.description",
            )}
          />
        ) : null}

        {isNotFound ? (
          <EmptyState title={t("adManagement:organizationalUnits.detail.notFound")} />
        ) : null}

        {organizationalUnit ? (
          <AdOrganizationalUnitMoveForm
            key={organizationalUnit.objectGuid}
            organizationalUnit={organizationalUnit}
            returnPath={returnPath}
            locationState={location.state}
          />
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
