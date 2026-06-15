import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useLocation, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AxiosError } from "axios";

import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import {
  getAdDeletedObjectPrimaryLabel,
  getAdDeletedObjectSecondaryLabel,
} from "@/features/ad-management/ad-deleted-object-display-labels";
import { getAdDeletedObjectTypeLabel } from "@/features/ad-management/ad-deleted-object-labels";
import { AD_DELETED_OBJECTS_LIST_PATH } from "@/features/ad-management/ad-deleted-objects-list-query";
import { resolveAdDeletedObjectReturnPath } from "@/features/ad-management/ad-deleted-objects-return-path";
import {
  AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY,
  getAdDeletedObjectById,
} from "@/features/ad-management/api";
import { AdUserDetailField } from "@/features/ad-management/components/ad-user-detail/AdUserDetailField";
import { AdDeletedObjectDetailHeaderActions } from "@/features/ad-management/components/AdDeletedObjectDetailHeaderActions";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { adDetailOutlineButtonClass } from "@/features/ad-management/ad-user-detail-button-styles";
import { getApiErrorMessage } from "@/lib/api-error";

export function AdDeletedObjectDetailPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const location = useLocation();
  const moduleStatus = useAdManagementModuleStatus();
  const hasValidId = Boolean(id?.trim()) && isGuidLike(id);
  const returnPath = resolveAdDeletedObjectReturnPath(location.state, AD_DELETED_OBJECTS_LIST_PATH);

  const detailQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY, "detail", id],
    queryFn: () => getAdDeletedObjectById(id!),
    enabled: hasValidId && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const detail = detailQuery.data;
  const primaryLabel = detail ? getAdDeletedObjectPrimaryLabel(detail) : null;
  const secondaryLabel =
    detail && primaryLabel ? getAdDeletedObjectSecondaryLabel(detail, primaryLabel) : null;

  const pageTitle = primaryLabel ?? t("adManagement:deletedObjects.detail.pageTitle");
  const pageDescription = useMemo(() => {
    if (!detail) {
      return t("adManagement:deletedObjects.detail.pageDescription");
    }

    return secondaryLabel ?? detail.distinguishedName;
  }, [detail, secondaryLabel, t]);

  const additionalAttributeEntries = useMemo(
    () =>
      Object.entries(detail?.additionalAttributes ?? {}).sort(([left], [right]) =>
        left.localeCompare(right),
      ),
    [detail?.additionalAttributes],
  );

  if (!hasValidId) {
    return (
      <AdManagementModuleStateGuard>
        <ErrorState
          title={t("adManagement:deletedObjects.errors.notFound")}
          description={t("adManagement:deletedObjects.errors.detailFailed")}
          retry={
            <Link to={returnPath} className={adDetailOutlineButtonClass}>
              {t("common:actions.back")}
            </Link>
          }
        />
      </AdManagementModuleStateGuard>
    );
  }

  if (moduleStatus.isOperational && detailQuery.isLoading) {
    return (
      <AdManagementModuleStateGuard>
        <LoadingState />
      </AdManagementModuleStateGuard>
    );
  }

  if (moduleStatus.isOperational && detailQuery.isError) {
    const isNotFound =
      detailQuery.error instanceof AxiosError && detailQuery.error.response?.status === 404;

    return (
      <AdManagementModuleStateGuard>
        <ErrorState
          title={
            isNotFound
              ? t("adManagement:deletedObjects.errors.notFound")
              : t("adManagement:deletedObjects.errors.detailFailed")
          }
          description={getApiErrorMessage(
            detailQuery.error,
            t("adManagement:deletedObjects.errors.detailFailed"),
          )}
          retry={
            <Link to={returnPath} className={adDetailOutlineButtonClass}>
              {t("common:actions.back")}
            </Link>
          }
        />
      </AdManagementModuleStateGuard>
    );
  }

  if (!detail) {
    return null;
  }

  return (
    <AdManagementModuleStateGuard>
      <section className="space-y-4">
        <PageHeader
          title={pageTitle}
          description={pageDescription}
          actions={
            <AdDeletedObjectDetailHeaderActions
              detail={detail}
              returnPath={returnPath}
              isFetching={detailQuery.isFetching}
              onRefresh={() => detailQuery.refetch()}
            />
          }
        />

        <SectionCard title={t("adManagement:deletedObjects.sections.summary")}>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">
                {t("adManagement:deletedObjects.table.type")}
              </p>
              <Badge variant="outline">
                {getAdDeletedObjectTypeLabel(t, detail.objectType)}
              </Badge>
            </div>
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.name")}
              value={detail.name}
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.displayName")}
              value={detail.displayName}
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.samAccountName")}
              value={detail.samAccountName}
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.userPrincipalName")}
              value={detail.userPrincipalName}
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.description")}
              value={detail.description}
            />
          </div>
        </SectionCard>

        <SectionCard title={t("adManagement:deletedObjects.sections.location")}>
          <div className="grid gap-4 md:grid-cols-2">
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.distinguishedName")}
              value={detail.distinguishedName}
              valueClassName="break-all font-mono text-xs"
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.lastKnownParent")}
              value={detail.lastKnownParent}
              valueClassName="break-all font-mono text-xs"
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.lastKnownRdn")}
              value={detail.lastKnownRdn}
            />
          </div>
        </SectionCard>

        <SectionCard title={t("adManagement:deletedObjects.sections.technical")}>
          <div className="grid gap-4 md:grid-cols-2">
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.objectGuid")}
              value={detail.id}
              valueClassName="break-all font-mono text-xs"
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.objectSid")}
              value={detail.objectSid}
              valueClassName="break-all font-mono text-xs"
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.objectClass")}
              value={detail.objectClass.join(", ")}
              valueClassName="break-all font-mono text-xs"
            />
            <AdUserDetailField label={t("adManagement:deletedObjects.fields.whenCreated")}>
              {detail.whenCreated ? <DateTimeText value={detail.whenCreated} /> : "-"}
            </AdUserDetailField>
            <AdUserDetailField label={t("adManagement:deletedObjects.fields.whenChanged")}>
              {detail.whenChanged ? <DateTimeText value={detail.whenChanged} /> : "-"}
            </AdUserDetailField>
          </div>
        </SectionCard>

        <SectionCard title={t("adManagement:deletedObjects.sections.additionalAttributes")}>
          <div className="grid gap-4 md:grid-cols-2">
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.mail")}
              value={detail.mail}
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.department")}
              value={detail.department}
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.dnsHostName")}
              value={detail.dnsHostName}
            />
            <AdUserDetailField
              label={t("adManagement:deletedObjects.fields.operatingSystem")}
              value={detail.operatingSystem}
            />
            <div className="space-y-2 md:col-span-2">
              <p className="text-xs text-muted-foreground">
                {t("adManagement:deletedObjects.fields.memberOf")}
              </p>
              {detail.memberOf.length ? (
                <div className="space-y-2">
                  {detail.memberOfTruncated ? (
                    <p className="text-sm text-muted-foreground">
                      {t("adManagement:groups.detail.truncatedNotice")}
                    </p>
                  ) : null}
                  <div className="divide-y rounded-lg border">
                    {detail.memberOf.map((memberDn) => (
                      <p
                        key={memberDn}
                        className="break-all px-3 py-2 font-mono text-xs text-muted-foreground"
                        title={memberDn}
                      >
                        {memberDn}
                      </p>
                    ))}
                  </div>
                </div>
              ) : (
                <EmptyState title={t("adManagement:groups.detail.memberOfEmpty")} />
              )}
            </div>
            {additionalAttributeEntries.map(([key, value]) => (
              <AdUserDetailField
                key={key}
                label={key}
                value={value}
                valueClassName="break-all font-mono text-xs"
              />
            ))}
          </div>
        </SectionCard>
      </section>
    </AdManagementModuleStateGuard>
  );
}
