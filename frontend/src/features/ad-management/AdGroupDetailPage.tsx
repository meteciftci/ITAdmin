import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
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
import { Badge } from "@/components/ui/badge";
import { RowActions } from "@/components/common/RowActions";
import { Button } from "@/components/ui/button";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/dropdown-menu";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import {
  getAdGroupMemberPrimaryLabel,
  getAdGroupMemberSecondaryLabel,
  getAdGroupPrimaryLabel,
  getAdGroupSecondaryLabel,
} from "@/features/ad-management/ad-group-display-labels";
import {
  getAdGroupMemberTypeLabel,
  getAdGroupScopeLabel,
  getAdGroupTypeLabel,
} from "@/features/ad-management/ad-group-labels";
import { buildAdGroupEditPath } from "@/features/ad-management/ad-group-detail-path";
import { adDetailActionButtonSizingClass, adDetailEditButtonClass, adDetailOutlineButtonClass } from "@/features/ad-management/ad-user-detail-button-styles";
import {
  buildAdGroupDetailReturnState,
  resolveAdGroupReturnPath,
} from "@/features/ad-management/ad-groups-return-path";
import { AD_GROUPS_LIST_PATH } from "@/features/ad-management/ad-groups-list-path";
import { AD_MANAGEMENT_GROUPS_QUERY_KEY, getAdGroupById } from "@/features/ad-management/api";
import { AdDeleteGroupConfirmDialog } from "@/features/ad-management/components/AdDeleteGroupConfirmDialog";
import { AdGroupMembersSection } from "@/features/ad-management/components/AdGroupMembersSection";
import { AdUserDetailField } from "@/features/ad-management/components/ad-user-detail/AdUserDetailField";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type { AdGroupMemberItem } from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

function MemberList({
  items,
  truncated,
  emptyTitle,
}: {
  items: AdGroupMemberItem[];
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
          {t("groups.detail.truncatedNotice")}
        </p>
      ) : null}
      <div className="divide-y rounded-lg border">
        {items.map((item) => {
          const primaryLabel = getAdGroupMemberPrimaryLabel(item);
          const secondaryLabel = getAdGroupMemberSecondaryLabel(item, primaryLabel);

          return (
            <div
              key={item.distinguishedName}
              className="space-y-1 px-3 py-3"
              title={item.distinguishedName}
            >
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-medium" title={item.distinguishedName}>
                  {primaryLabel}
                </p>
                <Badge variant="outline">{getAdGroupMemberTypeLabel(t, item.type)}</Badge>
              </div>
              {secondaryLabel ? (
                <p className="truncate text-xs text-muted-foreground" title={secondaryLabel}>
                  {secondaryLabel}
                </p>
              ) : null}
              {item.description ? (
                <p
                  className="line-clamp-2 text-xs text-muted-foreground"
                  title={item.description}
                >
                  {item.description}
                </p>
              ) : null}
            </div>
          );
        })}
      </div>
    </div>
  );
}

export function AdGroupDetailPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const currentUser = useAuthStore((state) => state.user);
  const canUpdateGroup = canAccess(currentUser, "AdManagement.Groups.Update");
  const canDeleteGroup = canAccess(currentUser, "AdManagement.Groups.Delete");
  const canManageMembers = canAccess(currentUser, "AdManagement.Groups.ManageMembers");
  const { id: groupId } = useParams<{ id: string }>();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const membersSectionRef = useRef<HTMLDivElement>(null);
  const lastMembersScrollKeyRef = useRef<string | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const moduleStatus = useAdManagementModuleStatus();
  const hasValidId = Boolean(groupId?.trim()) && isGuidLike(groupId);
  const returnPath = resolveAdGroupReturnPath(location.state, AD_GROUPS_LIST_PATH);

  const groupQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_GROUPS_QUERY_KEY, "detail", groupId],
    queryFn: () => getAdGroupById(groupId!),
    enabled: hasValidId && moduleStatus.isOperational,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const group = groupQuery.data;
  const primaryLabel = group ? getAdGroupPrimaryLabel(group) : null;
  const secondaryLabel = group && primaryLabel
    ? getAdGroupSecondaryLabel(group, primaryLabel)
    : null;

  const pageTitle = primaryLabel ?? t("adManagement:groups.detail.pageTitle");
  const pageDescription = useMemo(() => {
    if (!group) {
      return undefined;
    }

    return secondaryLabel ?? group.samAccountName ?? group.distinguishedName;
  }, [group, secondaryLabel]);

  const scrollToMembersSection = useCallback(() => {
    membersSectionRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
    membersSectionRef.current?.focus({ preventScroll: true });
  }, []);

  useEffect(() => {
    if (!group || searchParams.get("section") !== "members") {
      return;
    }

    const navigationKey = `${location.key}:members`;
    if (lastMembersScrollKeyRef.current === navigationKey) {
      return;
    }

    lastMembersScrollKeyRef.current = navigationKey;
    const frameId = requestAnimationFrame(() => {
      scrollToMembersSection();
    });

    return () => {
      cancelAnimationFrame(frameId);
    };
  }, [group, location.key, scrollToMembersSection, searchParams]);

  const isNotFound =
    groupQuery.isError
    && groupQuery.error instanceof AxiosError
    && groupQuery.error.response?.status === 404;

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

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={pageTitle}
          description={pageDescription}
          actions={
            <div className="flex flex-wrap items-center gap-2">
              <Link
                to={returnPath}
                className={adDetailOutlineButtonClass}
              >
                {t("adManagement:groups.actions.back")}
              </Link>
              <Button
                type="button"
                variant="outline"
                size="sm"
                className={adDetailActionButtonSizingClass}
                onClick={() => groupQuery.refetch()}
                disabled={groupQuery.isFetching}
              >
                {t("common:actions.refresh")}
              </Button>
              {canUpdateGroup && group ? (
                <Link
                  to={buildAdGroupEditPath(group.id)}
                  state={buildAdGroupDetailReturnState(group.id)}
                  className={adDetailEditButtonClass}
                >
                  {t("adManagement:groups.actions.edit")}
                </Link>
              ) : null}
              {(canManageMembers || canDeleteGroup) && group ? (
                <RowActions label={t("adManagement:groups.detail.actions.operations")}>
                  {canManageMembers ? (
                    <DropdownMenuItem onClick={scrollToMembersSection}>
                      {t("adManagement:groups.actions.manageMembers")}
                    </DropdownMenuItem>
                  ) : null}
                  {canDeleteGroup ? (
                    <>
                      {canManageMembers ? <DropdownMenuSeparator /> : null}
                      <DropdownMenuItem
                        className="text-destructive focus:text-destructive"
                        onClick={() => setDeleteDialogOpen(true)}
                      >
                        {t("adManagement:groups.actions.delete")}
                      </DropdownMenuItem>
                    </>
                  ) : null}
                </RowActions>
              ) : null}
            </div>
          }
        />

        {groupQuery.isLoading ? <LoadingState /> : null}

        {groupQuery.isError && !isNotFound ? (
          <ErrorState
            title={t("errors:generic.title")}
            description={getApiErrorMessage(groupQuery.error, t("errors:generic.description"))}
          />
        ) : null}

        {isNotFound ? (
          <EmptyState
            title={t("adManagement:groups.errors.notFound")}
            description={t("adManagement:groups.errors.notFound")}
          />
        ) : null}

        {group ? (
          <>
            <SectionCard title={t("adManagement:groups.detail.summaryTitle")}>
              <div className="grid gap-3 md:grid-cols-2">
                <AdUserDetailField
                  label={t("adManagement:groups.table.displayName")}
                  value={group.displayName}
                />
                <AdUserDetailField
                  label={t("adManagement:groups.table.name")}
                  value={group.name}
                />
                <AdUserDetailField
                  label={t("adManagement:groups.table.cn")}
                  value={group.cn}
                />
                <AdUserDetailField
                  label={t("adManagement:groups.table.samAccountName")}
                  value={group.samAccountName}
                />
                <AdUserDetailField
                  label={t("adManagement:groups.table.description")}
                  value={group.description}
                />
                <AdUserDetailField label={t("adManagement:groups.table.scope")}>
                  <Badge variant="secondary">
                    {getAdGroupScopeLabel(t, group.groupScope)}
                  </Badge>
                </AdUserDetailField>
                <AdUserDetailField label={t("adManagement:groups.detail.securityEnabled")}>
                  <Badge variant={group.securityEnabled ? "default" : "outline"}>
                    {getAdGroupTypeLabel(t, group.securityEnabled)}
                  </Badge>
                </AdUserDetailField>
              </div>
            </SectionCard>

            <SectionCard title={t("adManagement:groups.detail.technicalTitle")}>
              <div className="grid gap-3 md:grid-cols-2">
                <AdUserDetailField
                  label={t("adManagement:groups.detail.objectGuid")}
                  value={group.id}
                  valueClassName="break-all font-mono text-xs"
                />
                <AdUserDetailField
                  label={t("adManagement:groups.table.distinguishedName")}
                  value={group.distinguishedName}
                  valueClassName="break-all font-mono text-xs"
                />
                <AdUserDetailField
                  label={t("adManagement:groups.detail.groupType")}
                  value={
                    group.groupType === null || group.groupType === undefined
                      ? null
                      : String(group.groupType)
                  }
                  valueClassName="font-mono text-xs"
                />
                <AdUserDetailField label={t("adManagement:groups.detail.whenCreated")}>
                  <DateTimeText value={group.whenCreated} />
                </AdUserDetailField>
                <AdUserDetailField label={t("adManagement:groups.detail.whenChanged")}>
                  <DateTimeText value={group.whenChanged} />
                </AdUserDetailField>
                <AdUserDetailField
                  label={t("adManagement:groups.detail.managedBy")}
                  value={group.managedByDisplayName ?? group.managedByDistinguishedName}
                  valueClassName="break-all"
                />
                {group.managedByDisplayName && group.managedByDistinguishedName ? (
                  <AdUserDetailField
                    label={t("adManagement:groups.detail.managedByDn")}
                    value={group.managedByDistinguishedName}
                    valueClassName="break-all font-mono text-xs"
                  />
                ) : null}
              </div>
            </SectionCard>

            <AdGroupMembersSection
              ref={membersSectionRef}
              groupId={group.id}
              groupName={primaryLabel}
              memberCount={group.memberCount}
              canManageMembers={canManageMembers}
              enabled={moduleStatus.isOperational}
            />

            <SectionCard
              title={t("adManagement:groups.detail.memberOfTitle")}
              description={t("adManagement:groups.detail.memberOfCount", {
                count: group.memberOfCount,
              })}
            >
              <MemberList
                items={group.memberOf}
                truncated={group.memberOfTruncated}
                emptyTitle={t("adManagement:groups.detail.memberOfEmpty")}
              />
            </SectionCard>
          </>
        ) : null}
      </section>

      <AdDeleteGroupConfirmDialog
        open={deleteDialogOpen}
        groupId={group?.id ?? null}
        onOpenChange={setDeleteDialogOpen}
        onDeleted={() => {
          navigate(AD_GROUPS_LIST_PATH);
        }}
      />
    </AdManagementModuleStateGuard>
  );
}
