import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AxiosError } from "axios";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { RowActions } from "@/components/common/RowActions";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/dropdown-menu";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import { adDetailOutlineButtonClass } from "@/features/ad-management/ad-user-detail-button-styles";
import { buildAdOrganizationalUnitDetailPath } from "@/features/ad-management/ad-ou-detail-path";
import { resolveAdOrganizationalUnitsReturnPath } from "@/features/ad-management/ad-ous-return-path";
import {
  AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY,
  getAdOrganizationalUnitById,
} from "@/features/ad-management/api";
import {
  AdCreateOrganizationalUnitDialog,
  AdDeleteOrganizationalUnitDialog,
  AdMoveOrganizationalUnitDialog,
  AdRenameOrganizationalUnitDialog,
} from "@/features/ad-management/components/AdOrganizationalUnitDialogs";
import { AdOrganizationalUnitRecentOperationsSection } from "@/features/ad-management/components/AdOrganizationalUnitRecentOperationsSection";
import { AdUserDetailField } from "@/features/ad-management/components/ad-user-detail/AdUserDetailField";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";

export function AdOrganizationalUnitDetailPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();

  const canCreate = canAccess(currentUser, "AdManagement.OrganizationalUnits.Create");
  const canUpdate = canAccess(currentUser, "AdManagement.OrganizationalUnits.Update");
  const canMove = canAccess(currentUser, "AdManagement.OrganizationalUnits.Move");
  const canDelete = canAccess(currentUser, "AdManagement.OrganizationalUnits.Delete");
  const canViewOperationLogs = canAccess(currentUser, "AdOperationLogs.View");

  const [createOpen, setCreateOpen] = useState(false);
  const [renameOpen, setRenameOpen] = useState(false);
  const [moveOpen, setMoveOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const organizationalUnitId = id?.trim() ?? "";
  const hasValidId = isGuidLike(organizationalUnitId);
  const returnPath = resolveAdOrganizationalUnitsReturnPath(location.state);

  const detailQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY, "detail", organizationalUnitId],
    queryFn: () => getAdOrganizationalUnitById(organizationalUnitId),
    enabled: moduleStatus.isOperational && hasValidId,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const organizationalUnit = detailQuery.data;
  const title = useMemo(() => {
    if (!organizationalUnit) {
      return t("adManagement:organizationalUnits.detail.title");
    }

    return organizationalUnit.name?.trim()
      || organizationalUnit.ou?.trim()
      || organizationalUnit.canonicalName;
  }, [organizationalUnit, t]);

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
          title={title}
          description={organizationalUnit?.canonicalName}
          actions={
            organizationalUnit ? (
              <div className="flex flex-wrap items-center gap-2">
                <Link to={returnPath} className={adDetailOutlineButtonClass}>
                  {t("common:actions.back")}
                </Link>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => detailQuery.refetch()}
                  disabled={detailQuery.isFetching}
                >
                  {t("common:actions.refresh")}
                </Button>
                <RowActions>
                  {canCreate ? (
                    <DropdownMenuItem onClick={() => setCreateOpen(true)}>
                      {t("adManagement:organizationalUnits.actions.createChild")}
                    </DropdownMenuItem>
                  ) : null}
                  {canUpdate ? (
                    <DropdownMenuItem onClick={() => setRenameOpen(true)}>
                      {t("adManagement:organizationalUnits.actions.rename")}
                    </DropdownMenuItem>
                  ) : null}
                  {canMove ? (
                    <DropdownMenuItem onClick={() => setMoveOpen(true)}>
                      {t("adManagement:organizationalUnits.actions.move")}
                    </DropdownMenuItem>
                  ) : null}
                  {canDelete ? (
                    <>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem
                        className="text-destructive focus:text-destructive"
                        onClick={() => setDeleteOpen(true)}
                      >
                        {t("common:actions.delete")}
                      </DropdownMenuItem>
                    </>
                  ) : null}
                </RowActions>
              </div>
            ) : null
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
          <>
            <SectionCard title={t("adManagement:organizationalUnits.detail.sections.overview")}>
              <div className="grid gap-4 md:grid-cols-2">
                <AdUserDetailField
                  label={t("adManagement:organizationalUnits.fields.canonicalName")}
                  value={organizationalUnit.canonicalName}
                />
                <AdUserDetailField
                  label={t("adManagement:organizationalUnits.fields.distinguishedName")}
                  value={organizationalUnit.distinguishedName}
                  valueClassName="break-all font-mono text-xs"
                />
                <AdUserDetailField
                  label={t("adManagement:organizationalUnits.fields.parent")}
                  value={organizationalUnit.parentDistinguishedName}
                  valueClassName="break-all font-mono text-xs"
                />
                <AdUserDetailField
                  label={t("adManagement:organizationalUnits.fields.displayName")}
                  value={organizationalUnit.displayName}
                />
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:organizationalUnits.detail.sections.contentSummary")}>
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <AdUserDetailField
                  label={t("adManagement:organizationalUnits.table.childOuCount")}
                  value={String(organizationalUnit.contentSummary.childOuCount)}
                />
                <AdUserDetailField
                  label={t("adManagement:organizationalUnits.table.userCount")}
                  value={String(organizationalUnit.contentSummary.userCount)}
                />
                <AdUserDetailField
                  label={t("adManagement:organizationalUnits.table.groupCount")}
                  value={String(organizationalUnit.contentSummary.groupCount)}
                />
                <AdUserDetailField
                  label={t("adManagement:organizationalUnits.table.computerCount")}
                  value={String(organizationalUnit.contentSummary.computerCount)}
                />
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:organizationalUnits.detail.sections.childOrganizationalUnits")}>
              {organizationalUnit.childOrganizationalUnits.length === 0 ? (
                <EmptyState title={t("adManagement:organizationalUnits.detail.noChildOrganizationalUnits")} />
              ) : (
                <div className="divide-y rounded-lg border">
                  {organizationalUnit.childOrganizationalUnits.map((child) => (
                    <div key={child.objectGuid} className="flex items-center justify-between gap-3 px-3 py-3">
                      <div className="min-w-0">
                        <p className="font-medium">
                          {child.name?.trim() || child.ou?.trim() || child.canonicalName}
                        </p>
                        <p className="truncate text-xs text-muted-foreground">{child.canonicalName}</p>
                      </div>
                      <Link
                        to={buildAdOrganizationalUnitDetailPath(child.objectGuid)}
                        className={adDetailOutlineButtonClass}
                      >
                        {t("common:actions.detail")}
                      </Link>
                    </div>
                  ))}
                </div>
              )}
            </SectionCard>

            {canViewOperationLogs ? (
              <AdOrganizationalUnitRecentOperationsSection
                organizationalUnitId={organizationalUnit.objectGuid}
                enabled
              />
            ) : null}
          </>
        ) : null}
      </section>

      {organizationalUnit ? (
        <>
          <AdCreateOrganizationalUnitDialog
            open={createOpen}
            defaultParentDistinguishedName={organizationalUnit.distinguishedName}
            onOpenChange={setCreateOpen}
          />
          <AdRenameOrganizationalUnitDialog
            open={renameOpen}
            organizationalUnit={organizationalUnit}
            onOpenChange={setRenameOpen}
            onSuccess={() => detailQuery.refetch()}
          />
          <AdMoveOrganizationalUnitDialog
            open={moveOpen}
            organizationalUnit={organizationalUnit}
            onOpenChange={setMoveOpen}
            onSuccess={() => detailQuery.refetch()}
          />
          <AdDeleteOrganizationalUnitDialog
            open={deleteOpen}
            organizationalUnit={organizationalUnit}
            onOpenChange={setDeleteOpen}
            onDeleted={() => navigate(returnPath)}
          />
        </>
      ) : null}
    </AdManagementModuleStateGuard>
  );
}
