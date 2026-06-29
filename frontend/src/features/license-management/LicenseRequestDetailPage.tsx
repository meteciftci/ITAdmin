import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { useAuthStore } from "@/features/auth/auth-store";
import { getLicenseRequestById } from "@/features/license-management/api";
import { LicenseDetailField } from "@/features/license-management/components/LicenseDetailField";
import { LicenseRequestStatusBadge } from "@/features/license-management/components/LicenseRequestStatusBadge";
import { LicenseRequestUserSnapshot } from "@/features/license-management/components/LicenseRequestUserSnapshot";
import {
  getRequestItemStatusLabel,
  getRequestSourceLabel,
} from "@/features/license-management/enum-labels";
import {
  buildLicenseRequestEditPath,
  LICENSE_REQUESTS_LIST_PATH,
} from "@/features/license-management/license-request-paths";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

function DateOnlyField({ label, value }: { label: string; value: string | null }) {
  return (
    <LicenseDetailField label={label}>
      {value ? (
        <DateTimeText value={value} options={{ year: "numeric", month: "2-digit", day: "2-digit" }} />
      ) : (
        <span className="text-sm">-</span>
      )}
    </LicenseDetailField>
  );
}

export function LicenseRequestDetailPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageRequests);

  const detailQuery = useQuery({
    queryKey: ["license-management", "requests", "detail", id],
    queryFn: () => getLicenseRequestById(id!),
    enabled: Boolean(id),
  });

  if (!id) {
    return (
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <EmptyState title={t("licenseManagement:pages.requests.detail.notFound")} />
      </section>
    );
  }

  const request = detailQuery.data;
  const isNotFound =
    detailQuery.isError
    && detailQuery.error instanceof AxiosError
    && detailQuery.error.response?.status === 404;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={request?.requestNumber ?? t("licenseManagement:pages.requests.detail.title")}
        description={t("licenseManagement:pages.requests.detail.description")}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Link to={LICENSE_REQUESTS_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
              {t("common:actions.back")}
            </Link>
            <Button type="button" variant="outline" onClick={() => detailQuery.refetch()} disabled={detailQuery.isFetching}>
              {t("common:actions.refresh")}
            </Button>
            {canManage && request ? (
              <Link to={buildLicenseRequestEditPath(request.id)} className={cn(buttonVariants())}>
                {t("common:actions.edit")}
              </Link>
            ) : null}
          </div>
        }
      />

      {detailQuery.isLoading ? <LoadingState /> : null}
      {detailQuery.isError && !isNotFound ? (
        <ErrorState
          title={t("errors:generic.title")}
          description={getApiErrorMessage(detailQuery.error, t("errors:generic.description"))}
        />
      ) : null}
      {isNotFound ? <EmptyState title={t("licenseManagement:pages.requests.detail.notFound")} /> : null}

      {request ? (
        <div className="space-y-4">
          <SectionCard title={t("licenseManagement:pages.requests.detail.summaryTitle")}>
            <div className="grid gap-4 md:grid-cols-2">
              <LicenseDetailField label={t("licenseManagement:requests.fields.requestNumber")}>
                {request.requestNumber}
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:requests.fields.requestSource")}>
                {getRequestSourceLabel(t, request.requestSource)}
              </LicenseDetailField>
              <DateOnlyField label={t("licenseManagement:requests.fields.requestDate")} value={request.requestDate} />
              <LicenseDetailField label={t("common:fields.status")}>
                <LicenseRequestStatusBadge status={request.status} />
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:requests.fields.externalRequestNumber")}>
                {request.externalRequestNumber ?? "-"}
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:requests.fields.ebysNumber")}>
                {request.ebysNumber ?? "-"}
              </LicenseDetailField>
              <DateOnlyField label={t("licenseManagement:requests.fields.ebysDate")} value={request.ebysDate} />
              <LicenseDetailField label={t("licenseManagement:requests.fields.requesterUnit")}>
                {request.requesterUnit ?? "-"}
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:requests.fields.requestedByManagerName")}>
                {request.requestedByManagerName ?? "-"}
              </LicenseDetailField>
              <LicenseDetailField label={t("common:fields.description")}>
                {request.description ?? "-"}
              </LicenseDetailField>
            </div>
          </SectionCard>

          <SectionCard title={t("licenseManagement:requests.sections.requester")}>
            <LicenseRequestUserSnapshot
              snapshot={{
                adObjectId: request.requestedByAdObjectId,
                samAccountName: request.requestedBySamAccountName,
                userPrincipalName: request.requestedByUserPrincipalName,
                displayName: request.requestedByDisplayName,
                department: request.requestedByDepartment,
                title: request.requestedByTitle,
                mail: request.requestedByMail,
                phone: request.requestedByPhone,
              }}
            />
          </SectionCard>

          <SectionCard title={t("licenseManagement:requests.sections.items")}>
            <div className="space-y-4">
              {request.items.map((item) => (
                <div key={item.id} className="space-y-3 rounded-lg border bg-card p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h3 className="text-sm font-semibold">{item.productName}</h3>
                    <span className="text-sm text-muted-foreground">
                      {getRequestItemStatusLabel(t, item.status)}
                    </span>
                  </div>
                  <div className="grid gap-3 md:grid-cols-2">
                    <LicenseDetailField label={t("licenseManagement:requests.fields.requestedUserCount", { count: item.requestedQuantity })}>
                      {item.requestedQuantity}
                    </LicenseDetailField>
                    <LicenseDetailField label={t("licenseManagement:requests.fields.estimatedTotalCost")}>
                      {item.estimatedTotalCost != null
                        ? `${item.estimatedTotalCost}${item.currency ? ` ${item.currency}` : ""}`
                        : "-"}
                    </LicenseDetailField>
                    <LicenseDetailField label={t("licenseManagement:requests.fields.justification")}>
                      {item.justification ?? "-"}
                    </LicenseDetailField>
                  </div>
                  <div className="space-y-2">
                    <p className="text-sm font-medium">{t("licenseManagement:requests.fields.selectedUsers")}</p>
                    <ul className="space-y-1 text-sm text-muted-foreground">
                      {item.users.map((userItem) => (
                        <li key={userItem.id}>
                          {userItem.displayName || userItem.samAccountName || userItem.userPrincipalName}
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              ))}
            </div>
          </SectionCard>

          <SectionCard title={t("licenseManagement:requests.sections.cost")}>
            <div className="grid gap-4 md:grid-cols-2">
              <LicenseDetailField label={t("licenseManagement:requests.fields.estimatedTotalCost")}>
                {request.estimatedTotalCost != null
                  ? `${request.estimatedTotalCost}${request.currency ? ` ${request.currency}` : ""}`
                  : "-"}
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:requests.fields.vatIncluded")}>
                {request.vatIncluded ? t("licenseManagement:boolean.yes") : t("licenseManagement:boolean.no")}
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:requests.fields.costNote")}>
                {request.costNote ?? "-"}
              </LicenseDetailField>
            </div>
          </SectionCard>

          <SectionCard title={t("licenseManagement:pages.detail.auditTitle")}>
            <div className="grid gap-4 md:grid-cols-2">
              <LicenseDetailField label={t("licenseManagement:pages.detail.createdAt")}>
                <DateTimeText value={request.createdAt} />
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:pages.detail.createdBy")}>
                {request.createdBy ?? "-"}
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:pages.detail.updatedAt")}>
                {request.updatedAt ? <DateTimeText value={request.updatedAt} /> : "-"}
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:pages.detail.updatedBy")}>
                {request.updatedBy ?? "-"}
              </LicenseDetailField>
            </div>
          </SectionCard>
        </div>
      ) : null}
    </section>
  );
}
