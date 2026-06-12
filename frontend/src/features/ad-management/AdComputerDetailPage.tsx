import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocation, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AxiosError } from "axios";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import {
  getAdComputerPrimaryLabel,
  getAdComputerSecondaryLabel,
} from "@/features/ad-management/ad-computer-display-labels";
import { AdComputerDetailHeaderActions } from "@/features/ad-management/components/AdComputerDetailHeaderActions";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import { resolveAdComputerReturnPath } from "@/features/ad-management/ad-computers-return-path";
import { AD_COMPUTERS_LIST_PATH } from "@/features/ad-management/ad-computers-list-path";
import { AD_MANAGEMENT_COMPUTERS_QUERY_KEY, getAdComputerById } from "@/features/ad-management/api";
import { AdComputerStatusBadge } from "@/features/ad-management/components/AdComputerStatusBadge";
import { AdUserDetailField } from "@/features/ad-management/components/ad-user-detail/AdUserDetailField";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type { AdComputerMemberOfItem } from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

function MemberOfList({
  items,
  truncated,
  emptyTitle,
}: {
  items: AdComputerMemberOfItem[];
  truncated: boolean;
  emptyTitle: string;
}) {
  const { t } = useTranslation("adManagement");

  if (!items.length) {
    return <EmptyState title={emptyTitle} />;
  }

  return (
    <div className="space-y-3">
      {truncated ? (
        <p
          className={cn(
            "rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-muted-foreground",
          )}
        >
          {t("computers.detail.truncatedNotice")}
        </p>
      ) : null}
      <div className="divide-y rounded-lg border">
        {items.map((item) => {
          const primaryLabel = item.name?.trim()
            || item.samAccountName?.trim()
            || item.distinguishedName;

          return (
            <div
              key={item.distinguishedName}
              className="space-y-1 px-3 py-3"
              title={item.distinguishedName}
            >
              <p className="font-medium" title={item.distinguishedName}>
                {primaryLabel}
              </p>
              {item.samAccountName && item.samAccountName !== primaryLabel ? (
                <p className="truncate text-xs text-muted-foreground" title={item.samAccountName}>
                  {item.samAccountName}
                </p>
              ) : null}
            </div>
          );
        })}
      </div>
    </div>
  );
}

export function AdComputerDetailPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const { id: computerId } = useParams<{ id: string }>();
  const location = useLocation();
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();
  const canUpdateComputer = canAccess(currentUser, "AdManagement.Computers.Update");
  const canMoveOu = canAccess(currentUser, "AdManagement.Computers.MoveOu");
  const canEnableComputer = canAccess(currentUser, "AdManagement.Computers.Enable");
  const canDisableComputer = canAccess(currentUser, "AdManagement.Computers.Disable");
  const canDeleteComputer = canAccess(currentUser, "AdManagement.Computers.Delete");
  const canManageGroups = canAccess(currentUser, "AdManagement.Computers.Groups.View");
  const hasValidId = Boolean(computerId?.trim()) && isGuidLike(computerId);
  const returnPath = resolveAdComputerReturnPath(location.state, AD_COMPUTERS_LIST_PATH);

  const computerQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_COMPUTERS_QUERY_KEY, "detail", computerId],
    queryFn: () => getAdComputerById(computerId!),
    enabled: hasValidId && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const computer = computerQuery.data;
  const primaryLabel = computer ? getAdComputerPrimaryLabel(computer) : null;
  const secondaryLabel = computer && primaryLabel
    ? getAdComputerSecondaryLabel(computer, primaryLabel)
    : null;

  const pageTitle = primaryLabel ?? t("adManagement:computers.detail.pageTitle");
  const pageDescription = useMemo(() => {
    if (!computer) {
      return undefined;
    }

    return secondaryLabel ?? computer.dnsHostName ?? computer.distinguishedName;
  }, [computer, secondaryLabel]);

  const isNotFound =
    computerQuery.isError
    && computerQuery.error instanceof AxiosError
    && computerQuery.error.response?.status === 404;

  const summaryCards = computer ? (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      <div className="rounded-lg border bg-card px-4 py-3 shadow-sm">
        <p className="text-xs font-medium text-muted-foreground">
          {t("adManagement:computers.detail.accountStatusCard")}
        </p>
        <div className="mt-2 text-sm">
          <AdComputerStatusBadge isEnabled={computer.isEnabled} />
        </div>
      </div>
      <div className="rounded-lg border bg-card px-4 py-3 shadow-sm">
        <p className="text-xs font-medium text-muted-foreground">
          {t("adManagement:computers.detail.lastLogon")}
        </p>
        <div className="mt-2 text-sm">
          <DateTimeText value={computer.lastLogonAt} />
        </div>
      </div>
      <div className="rounded-lg border bg-card px-4 py-3 shadow-sm">
        <p className="text-xs font-medium text-muted-foreground">
          {t("adManagement:computers.detail.whenCreated")}
        </p>
        <div className="mt-2 text-sm">
          <DateTimeText value={computer.whenCreated} />
        </div>
      </div>
      <div className="rounded-lg border bg-card px-4 py-3 shadow-sm">
        <p className="text-xs font-medium text-muted-foreground">
          {t("adManagement:computers.detail.whenChanged")}
        </p>
        <div className="mt-2 text-sm">
          <DateTimeText value={computer.whenChanged} />
        </div>
      </div>
    </div>
  ) : null;

  const basicSection = computer ? (
    <SectionCard title={t("adManagement:computers.detail.summaryTitle")}>
      <div className="grid gap-3 md:grid-cols-2">
        <AdUserDetailField
          label={t("adManagement:computers.table.computer")}
          value={computer.name}
        />
        <AdUserDetailField
          label={t("adManagement:computers.table.samAccountName")}
          value={computer.samAccountName}
        />
        <AdUserDetailField
          label={t("adManagement:computers.table.dnsHostName")}
          value={computer.dnsHostName}
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.cn")}
          value={computer.cn}
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.description")}
          value={computer.description}
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.managedBy")}
          value={computer.managedByDisplayName ?? computer.managedByDistinguishedName}
          valueClassName="break-all"
        />
        {computer.managedByDisplayName && computer.managedByDistinguishedName ? (
          <AdUserDetailField
            label={t("adManagement:computers.detail.managedByDn")}
            value={computer.managedByDistinguishedName}
            valueClassName="break-all font-mono text-xs"
          />
        ) : null}
      </div>
    </SectionCard>
  ) : null;

  const operatingSystemSection = computer ? (
    <SectionCard title={t("adManagement:computers.detail.operatingSystemTitle")}>
      <div className="grid gap-3 md:grid-cols-2">
        <AdUserDetailField
          label={t("adManagement:computers.table.operatingSystem")}
          value={computer.operatingSystem}
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.operatingSystemVersion")}
          value={computer.operatingSystemVersion}
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.operatingSystemServicePack")}
          value={computer.operatingSystemServicePack}
        />
      </div>
    </SectionCard>
  ) : null;

  const technicalSection = computer ? (
    <SectionCard title={t("adManagement:computers.detail.technicalTitle")}>
      <div className="grid gap-3 md:grid-cols-2">
        <AdUserDetailField
          label={t("adManagement:computers.detail.objectGuid")}
          value={computer.id}
          valueClassName="break-all font-mono text-xs"
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.distinguishedName")}
          value={computer.distinguishedName}
          valueClassName="break-all font-mono text-xs"
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.parentOu")}
          value={computer.parentOuDistinguishedName}
          valueClassName="break-all font-mono text-xs"
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.userAccountControl")}
          value={
            computer.userAccountControl === null || computer.userAccountControl === undefined
              ? null
              : String(computer.userAccountControl)
          }
          valueClassName="font-mono text-xs"
        />
        <AdUserDetailField
          label={t("adManagement:computers.detail.primaryGroupId")}
          value={
            computer.primaryGroupId === null || computer.primaryGroupId === undefined
              ? null
              : String(computer.primaryGroupId)
          }
          valueClassName="font-mono text-xs"
        />
      </div>
    </SectionCard>
  ) : null;

  const memberOfSection = computer ? (
    <SectionCard
      title={t("adManagement:computers.detail.memberOfTitle")}
      description={t("adManagement:computers.detail.memberOfCount", {
        count: computer.memberOfCount,
      })}
    >
      <MemberOfList
        items={computer.memberOf}
        truncated={computer.memberOfTruncated}
        emptyTitle={t("adManagement:computers.detail.memberOfEmpty")}
      />
    </SectionCard>
  ) : null;

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

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={pageTitle}
          description={pageDescription}
          actions={
            computer ? (
              <AdComputerDetailHeaderActions
                computer={computer}
                returnPath={returnPath}
                isFetching={computerQuery.isFetching}
                onRefresh={() => computerQuery.refetch()}
                canUpdateComputer={canUpdateComputer}
                canMoveOu={canMoveOu}
                canEnableComputer={canEnableComputer}
                canDisableComputer={canDisableComputer}
                canDeleteComputer={canDeleteComputer}
                canManageGroups={canManageGroups}
              />
            ) : null
          }
        />

        {computerQuery.isLoading ? <LoadingState /> : null}

        {computerQuery.isError && !isNotFound ? (
          <ErrorState
            title={t("errors:generic.title")}
            description={getApiErrorMessage(computerQuery.error, t("errors:generic.description"))}
          />
        ) : null}

        {isNotFound ? (
          <EmptyState
            title={t("adManagement:computers.errors.notFound")}
            description={t("adManagement:computers.errors.notFound")}
          />
        ) : null}

        {computer ? (
          <>
            {summaryCards}
            {basicSection}
            {operatingSystemSection}
            {technicalSection}
            {memberOfSection}
          </>
        ) : null}
      </section>
    </AdManagementModuleStateGuard>
  );
}
