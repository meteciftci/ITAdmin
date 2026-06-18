import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AxiosError } from "axios";
import { ListTree, Monitor, Shield, Users } from "lucide-react";

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
import {
  buildAdOrganizationalUnitCreatePath,
  buildAdOrganizationalUnitDetailPath,
  buildAdOrganizationalUnitMovePath,
  buildAdOrganizationalUnitRenamePath,
} from "@/features/ad-management/ad-ou-detail-path";
import {
  getAdOrganizationalUnitParentPath,
  getAdOrganizationalUnitPrimaryLabel,
} from "@/features/ad-management/ad-ou-display-labels";
import {
  buildAdOrganizationalUnitDetailReturnState,
  resolveAdOrganizationalUnitsReturnPath,
} from "@/features/ad-management/ad-ous-return-path";
import {
  AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY,
  getAdOrganizationalUnitById,
} from "@/features/ad-management/api";
import { AdDeleteOrganizationalUnitDialog } from "@/features/ad-management/components/AdOrganizationalUnitDialogs";
import { AdOrganizationalUnitCountBadge } from "@/features/ad-management/components/AdOrganizationalUnitCountBadge";
import { AdOrganizationalUnitRecentOperationsSection } from "@/features/ad-management/components/AdOrganizationalUnitRecentOperationsSection";
import { AdOrganizationalUnitTechnicalField } from "@/features/ad-management/components/AdOrganizationalUnitTechnicalField";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";
import { PermissionCodes } from "@/lib/permission-codes";

export function AdOrganizationalUnitDetailPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();

  const canCreate = canAccess(currentUser, PermissionCodes.AdManagement.OrganizationalUnits.Create);
  const canUpdate = canAccess(currentUser, PermissionCodes.AdManagement.OrganizationalUnits.Update);
  const canMove = canAccess(currentUser, PermissionCodes.AdManagement.OrganizationalUnits.Move);
  const canDelete = canAccess(currentUser, PermissionCodes.AdManagement.OrganizationalUnits.Delete);
  const canViewOperationLogs = canAccess(currentUser, PermissionCodes.AdOperationLogs.View);

  const [deleteOpen, setDeleteOpen] = useState(false);

  const organizationalUnitId = id?.trim() ?? "";
  const hasValidId = isGuidLike(organizationalUnitId);
  const detailPath = buildAdOrganizationalUnitDetailPath(organizationalUnitId);
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

    return getAdOrganizationalUnitPrimaryLabel(organizationalUnit);
  }, [organizationalUnit, t]);

  const pageDescription = useMemo(() => {
    if (!organizationalUnit) {
      return undefined;
    }

    return (
      getAdOrganizationalUnitParentPath(organizationalUnit.canonicalName)
      || organizationalUnit.canonicalName
    );
  }, [organizationalUnit]);

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
          description={pageDescription}
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
                    <DropdownMenuItem
                      onClick={() =>
                        navigate(
                          buildAdOrganizationalUnitCreatePath(organizationalUnit.distinguishedName),
                          { state: buildAdOrganizationalUnitDetailReturnState(detailPath) },
                        )
                      }
                    >
                      {t("adManagement:organizationalUnits.actions.createChild")}
                    </DropdownMenuItem>
                  ) : null}
                  {canUpdate ? (
                    <DropdownMenuItem
                      onClick={() =>
                        navigate(buildAdOrganizationalUnitRenamePath(organizationalUnit.objectGuid), {
                          state: buildAdOrganizationalUnitDetailReturnState(detailPath),
                        })
                      }
                    >
                      {t("adManagement:organizationalUnits.actions.rename")}
                    </DropdownMenuItem>
                  ) : null}
                  {canMove ? (
                    <DropdownMenuItem
                      onClick={() =>
                        navigate(buildAdOrganizationalUnitMovePath(organizationalUnit.objectGuid), {
                          state: buildAdOrganizationalUnitDetailReturnState(detailPath),
                        })
                      }
                    >
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
                <AdOrganizationalUnitTechnicalField
                  label={t("adManagement:organizationalUnits.fields.parent")}
                  value={organizationalUnit.parentDistinguishedName}
                  fullWidth
                />
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:organizationalUnits.detail.sections.contentSummary")}>
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <AdOrganizationalUnitCountBadge
                  variant="card"
                  label={t("adManagement:organizationalUnits.table.childOuCount")}
                  value={organizationalUnit.contentSummary.childOuCount}
                  icon={ListTree}
                />
                <AdOrganizationalUnitCountBadge
                  variant="card"
                  label={t("adManagement:organizationalUnits.table.userCount")}
                  value={organizationalUnit.contentSummary.userCount}
                  icon={Users}
                />
                <AdOrganizationalUnitCountBadge
                  variant="card"
                  label={t("adManagement:organizationalUnits.table.groupCount")}
                  value={organizationalUnit.contentSummary.groupCount}
                  icon={Shield}
                />
                <AdOrganizationalUnitCountBadge
                  variant="card"
                  label={t("adManagement:organizationalUnits.table.computerCount")}
                  value={organizationalUnit.contentSummary.computerCount}
                  icon={Monitor}
                />
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:organizationalUnits.detail.sections.childOrganizationalUnits")}>
              {organizationalUnit.childOrganizationalUnits.length === 0 ? (
                <EmptyState title={t("adManagement:organizationalUnits.detail.noChildOrganizationalUnits")} />
              ) : (
                <div className="divide-y rounded-lg border">
                  {organizationalUnit.childOrganizationalUnits.map((child) => (
                    <div
                      key={child.objectGuid}
                      className="flex flex-col gap-3 px-3 py-3 sm:flex-row sm:items-start sm:justify-between"
                    >
                      <div className="min-w-0 flex-1">
                        <p className="font-medium break-words">
                          {getAdOrganizationalUnitPrimaryLabel(child)}
                        </p>
                        <p
                          className="mt-1 line-clamp-2 text-xs text-muted-foreground break-words [overflow-wrap:anywhere]"
                          title={child.canonicalName}
                        >
                          {child.canonicalName}
                        </p>
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
        <AdDeleteOrganizationalUnitDialog
          open={deleteOpen}
          organizationalUnit={organizationalUnit}
          onOpenChange={setDeleteOpen}
          onDeleted={() => navigate(returnPath)}
        />
      ) : null}
    </AdManagementModuleStateGuard>
  );
}
