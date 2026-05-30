import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate, useParams, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { useAuthStore } from "@/features/auth/auth-store";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { createAdUserGroupColumns } from "@/features/ad-management/ad-user-groups-columns";
import {
  addAdUserToGroup,
  AD_MANAGEMENT_USER_GROUPS_QUERY_KEY,
  getAdUserGroups,
  invalidateAdUserGroupsQuery,
  removeAdUserFromGroup,
} from "@/features/ad-management/api";
import {
  filterAdUserGroupMemberships,
  formatAdGroupSelectionPrimaryLabel,
} from "@/features/ad-management/ad-group-display";
import { AdGroupSearchCombobox } from "@/features/ad-management/components/AdGroupSearchCombobox";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type {
  AdGroupSearchItem,
  AdUserGroupMembershipItem,
} from "@/features/ad-management/types";
import { resolveSafeReturnPath } from "@/features/ad-management/ad-return-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

const DEFAULT_MEMBERSHIP_PAGE_SIZE = 10;
const MEMBERSHIP_PAGE_SIZE_OPTIONS = [10, 25, 50] as const;

export function AdUserGroupsPage() {
  const { t } = useTranslation(["adManagement", "common"]);
  const { id: userId } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const returnPath = resolveSafeReturnPath(searchParams.get("returnTo"));
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();

  const canAddGroup = canAccess(currentUser, "AdManagement.Users.Groups.Add");
  const canRemoveGroup = canAccess(currentUser, "AdManagement.Users.Groups.Remove");

  const [selectedGroup, setSelectedGroup] = useState<AdGroupSearchItem | null>(null);
  const [removeTarget, setRemoveTarget] = useState<AdUserGroupMembershipItem | null>(null);
  const [membershipSearch, setMembershipSearch] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_MEMBERSHIP_PAGE_SIZE);

  const groupsQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_USER_GROUPS_QUERY_KEY, userId],
    queryFn: () => getAdUserGroups(userId!),
    enabled: Boolean(userId) && moduleStatus.isOperational,
  });

  const memberGroupDns = useMemo(
    () =>
      new Set(
        (groupsQuery.data?.groups ?? []).map((group) => group.distinguishedName),
      ),
    [groupsQuery.data?.groups],
  );

  const allGroups = useMemo(
    () => groupsQuery.data?.groups ?? [],
    [groupsQuery.data?.groups],
  );

  const filteredGroups = useMemo(
    () => filterAdUserGroupMemberships(allGroups, membershipSearch),
    [allGroups, membershipSearch],
  );

  const totalCount = filteredGroups.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const safePageNumber = Math.min(Math.max(pageNumber, 1), totalPages);
  const rangeStart = totalCount === 0 ? 0 : (safePageNumber - 1) * pageSize + 1;
  const rangeEnd = totalCount === 0 ? 0 : Math.min(safePageNumber * pageSize, totalCount);

  const paginatedGroups = useMemo(() => {
    const start = (safePageNumber - 1) * pageSize;
    return filteredGroups.slice(start, start + pageSize);
  }, [filteredGroups, pageSize, safePageNumber]);

  const addMutation = useMutation({
    mutationFn: (groupDistinguishedName: string) =>
      addAdUserToGroup(userId!, { groupDistinguishedName }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(response.message || t("adManagement:users.groups.messages.operationFailed"));
        return;
      }

      toast.success(
        response.message || t("adManagement:users.groups.messages.membershipAdded"),
      );
      setSelectedGroup(null);
      await invalidateAdUserGroupsQuery(queryClient, userId!);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:users.groups.messages.operationFailed")),
      );
    },
  });

  const removeMutation = useMutation({
    mutationFn: (groupDistinguishedName: string) =>
      removeAdUserFromGroup(userId!, { groupDistinguishedName }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(response.message || t("adManagement:users.groups.messages.operationFailed"));
        return;
      }

      toast.success(
        response.message || t("adManagement:users.groups.messages.membershipRemoved"),
      );
      setRemoveTarget(null);
      await invalidateAdUserGroupsQuery(queryClient, userId!);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:users.groups.messages.operationFailed")),
      );
    },
  });

  const columns = useMemo(
    () =>
      createAdUserGroupColumns({
        t,
        canRemoveGroup,
        isRemovePending: removeMutation.isPending,
        onRemove: setRemoveTarget,
      }),
    [t, canRemoveGroup, removeMutation.isPending],
  );

  const table = useServerDataTable({
    data: paginatedGroups,
    columns,
    pageCount: totalPages,
    pageIndex: safePageNumber - 1,
    pageSize,
  });

  if (!userId) {
    return <Navigate to="/ad-management/users" replace />;
  }

  const userSummary = groupsQuery.data;

  return (
    <AdManagementModuleStateGuard>
      <div className="space-y-6">
        <PageHeader
          title={t("adManagement:users.groups.pageTitle")}
          description={t("adManagement:users.groups.pageDescription")}
          actions={
            <Link
              to={returnPath}
              className={cn(buttonVariants({ variant: "outline" }))}
            >
              {t("common:actions.back")}
            </Link>
          }
        />

        {groupsQuery.isLoading ? <LoadingState /> : null}

        {groupsQuery.isError ? (
          <ErrorState
            title={t("adManagement:users.groups.errors.loadFailed")}
            description={getApiErrorMessage(
              groupsQuery.error,
              t("adManagement:users.groups.errors.loadFailed"),
            )}
          />
        ) : null}

        {groupsQuery.isSuccess && userSummary ? (
          <>
            <SectionCard
              title={t("adManagement:users.groups.sections.userSummary")}
              description={t("adManagement:users.groups.pageHeading")}
            >
              <div className="grid gap-4 md:grid-cols-2">
                <SummaryField
                  label={t("adManagement:users.detail.displayName")}
                  value={userSummary.displayName}
                />
                <SummaryField
                  label={t("adManagement:users.detail.username")}
                  value={userSummary.samAccountName}
                />
                <SummaryField
                  label={t("adManagement:users.detail.upn")}
                  value={userSummary.userPrincipalName}
                />
                <SummaryField
                  label={t("adManagement:users.groups.table.distinguishedName")}
                  value={userSummary.distinguishedName}
                  mono
                />
              </div>
            </SectionCard>

            {canAddGroup ? (
              <SectionCard
                title={t("adManagement:users.groups.sections.addGroup")}
                description={t("adManagement:users.groups.sections.addGroupDescription")}
              >
                <div className="space-y-4">
                  <AdGroupSearchCombobox
                    value={selectedGroup}
                    onChange={setSelectedGroup}
                    disabledGroupDns={memberGroupDns}
                    disabled={addMutation.isPending}
                  />
                  {selectedGroup ? (
                    <div className="rounded-md border bg-muted/20 p-3 text-sm">
                      <p className="font-medium">
                        {formatAdGroupSelectionPrimaryLabel(selectedGroup)}
                      </p>
                      <p
                        className="mt-1 break-all font-mono text-xs text-muted-foreground"
                        title={selectedGroup.distinguishedName}
                      >
                        {selectedGroup.distinguishedName}
                      </p>
                    </div>
                  ) : null}
                  <Button
                    type="button"
                    disabled={!selectedGroup || addMutation.isPending}
                    onClick={() => {
                      if (!selectedGroup) {
                        return;
                      }

                      addMutation.mutate(selectedGroup.distinguishedName);
                    }}
                  >
                    {t("adManagement:users.groups.actions.addToGroup")}
                  </Button>
                </div>
              </SectionCard>
            ) : null}

            <SectionCard
              title={t("adManagement:users.groups.sections.currentMemberships")}
              description={t("adManagement:users.groups.sections.currentMembershipsDescription")}
            >
              {allGroups.length === 0 ? (
                <EmptyState
                  title={t("adManagement:users.groups.empty.noMembershipsTitle")}
                  description={t("adManagement:users.groups.empty.noMembershipsDescription")}
                />
              ) : (
                <div className="space-y-4">
                  <DataTableToolbar
                    searchValue={membershipSearch}
                    onSearchChange={(value) => {
                      setMembershipSearch(value);
                      setPageNumber(1);
                    }}
                    searchPlaceholder={t(
                      "adManagement:users.groups.fields.membershipSearchPlaceholder",
                    )}
                    showFiltersButton={false}
                    toolbarFooter={
                      totalCount > 0 ? (
                        <span className="text-sm text-muted-foreground">
                          {t("adManagement:users.groups.pagination.rangeInfo", {
                            start: rangeStart,
                            end: rangeEnd,
                            total: totalCount,
                          })}
                        </span>
                      ) : null
                    }
                  />

                  {filteredGroups.length === 0 ? (
                    <EmptyState
                      title={t("adManagement:users.groups.empty.searchNoResultsTitle")}
                      description={t(
                        "adManagement:users.groups.empty.searchNoResultsDescription",
                      )}
                    />
                  ) : (
                    <DataTable
                      table={table}
                      footer={
                        totalCount > 0 ? (
                          <DataTablePagination
                            mode="server"
                            pageNumber={safePageNumber}
                            pageSize={pageSize}
                            totalCount={totalCount}
                            totalPages={totalPages}
                            onPageChange={setPageNumber}
                            onPageSizeChange={(nextSize) => {
                              setPageSize(nextSize);
                              setPageNumber(1);
                            }}
                            pageSizeOptions={[...MEMBERSHIP_PAGE_SIZE_OPTIONS]}
                            showPageSize
                            showSummary={false}
                          />
                        ) : null
                      }
                    />
                  )}
                </div>
              )}
            </SectionCard>
          </>
        ) : null}
      </div>

      <ConfirmDialog
        open={removeTarget !== null}
        onOpenChange={(open) => {
          if (!open) {
            setRemoveTarget(null);
          }
        }}
        title={t("adManagement:users.groups.confirm.removeTitle")}
        description={t("adManagement:users.groups.confirm.removeDescription")}
        confirmText={t("adManagement:users.groups.confirm.removeConfirm")}
        cancelText={t("common:actions.cancel")}
        variant="danger"
        isLoading={removeMutation.isPending}
        onConfirm={() => {
          if (!removeTarget) {
            return;
          }

          removeMutation.mutate(removeTarget.distinguishedName);
        }}
      />
    </AdManagementModuleStateGuard>
  );
}

function SummaryField({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: string | null | undefined;
  mono?: boolean;
}) {
  return (
    <div className="space-y-1">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={cn(mono && "font-mono text-xs break-all")}>{value || "-"}</p>
    </div>
  );
}
