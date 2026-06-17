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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";
import { buildAdOrganizationalUnitDetailPath } from "@/features/ad-management/ad-ou-detail-path";
import {
  getAdOrganizationalUnitPrimaryLabel,
  resolveOrganizationalUnitRenameName,
} from "@/features/ad-management/ad-ou-display-labels";
import { resolveAdOrganizationalUnitsReturnPath } from "@/features/ad-management/ad-ous-return-path";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import {
  AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY,
  getAdOrganizationalUnitById,
  invalidateAdOrganizationalUnitQueries,
  renameAdOrganizationalUnit,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type { AdOrganizationalUnitDetail } from "@/features/ad-management/types";
import { cn } from "@/lib/utils";

type RenameFormProps = {
  organizationalUnit: AdOrganizationalUnitDetail;
  returnPath: string;
  locationState: unknown;
};

function AdOrganizationalUnitRenameForm({
  organizationalUnit,
  returnPath,
  locationState,
}: RenameFormProps) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const moduleStatus = useAdManagementModuleStatus();
  const detailPath = buildAdOrganizationalUnitDetailPath(organizationalUnit.objectGuid);

  const [name, setName] = useState(() => resolveOrganizationalUnitRenameName(organizationalUnit));

  const renameMutation = useMutation({
    mutationFn: () => renameAdOrganizationalUnit(organizationalUnit.objectGuid, { name: name.trim() }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(
          resolveAdManagementApiMessage(
            t,
            response,
            "adManagement:organizationalUnits.rename.messages.renameFailed",
          ),
        );
        return;
      }

      await invalidateAdOrganizationalUnitQueries(queryClient);
      toast.success(t("adManagement:organizationalUnits.rename.messages.renamed"));
      navigate(detailPath, { state: locationState });
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:organizationalUnits.rename.messages.renameFailed",
        ),
      );
    },
  });

  const canSubmit =
    moduleStatus.isOperational
    && name.trim().length > 0
    && !renameMutation.isPending;

  return (
    <SectionCard title={t("adManagement:organizationalUnits.rename.formTitle")}>
      <div className="space-y-4">
        <p className="text-sm text-muted-foreground">
          {t("adManagement:organizationalUnits.rename.description")}
        </p>

        <div className="space-y-2">
          <Label htmlFor="ou-rename-name">
            {t("adManagement:organizationalUnits.fields.name")}
          </Label>
          <Input
            id="ou-rename-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder={t("adManagement:organizationalUnits.fields.namePlaceholder")}
          />
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" disabled={!canSubmit} onClick={() => renameMutation.mutate()}>
            {t("common:actions.save")}
          </Button>
          <Link to={returnPath} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("common:actions.cancel")}
          </Link>
        </div>
      </div>
    </SectionCard>
  );
}

export function AdOrganizationalUnitRenamePage() {
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
          title={t("adManagement:organizationalUnits.rename.pageTitle")}
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
          <AdOrganizationalUnitRenameForm
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
