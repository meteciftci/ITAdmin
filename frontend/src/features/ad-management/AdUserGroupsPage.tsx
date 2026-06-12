import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate, useLocation, useParams, useSearchParams } from "react-router-dom";
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
  formatAdGroupSelectionSecondaryLabel,
} from "@/features/ad-management/ad-group-display";
import { AdGroupMultiSearchCombobox } from "@/features/ad-management/components/AdGroupMultiSearchCombobox";
import { AdMembershipSelectionChips } from "@/features/ad-management/components/AdMembershipSelectionChips";
import {
  notifySequentialAddResults,
  partitionSequentialAddResults,
  runSequentialMembershipAdd,
} from "@/features/ad-management/run-sequential-membership-add";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type {
  AdGroupSearchItem,
  AdUserGroupMembershipItem,
} from "@/features/ad-management/types";
import { resolveAdUserReturnPathFromLocation } from "@/features/ad-management/ad-return-path";
import { AD_USERS_LIST_PATH } from "@/features/ad-management/ad-users-list-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

const DEFAULT_MEMBERSHIP_PAGE_SIZE = 10;
const MEMBERSHIP_PAGE_SIZE_OPTIONS = [10, 25, 50] as const;

export function AdUserGroupsPage() {
  const { t } = useTranslation(["adManagement", "common"]);
  const { id: userId } = useParams<{ id: string }>();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const returnPath = resolveAdUserReturnPathFromLocation(
    location.state,
    searchParams,
    AD_USERS_LIST_PATH,
  );
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();

  const canAddGroup = canAccess(currentUser, "AdManagement.Users.Groups.Add");
  const canRemoveGroup = canAccess(currentUser, "AdManagement.Users.Groups.Remove");

  const [selectedGroups, setSelectedGroups] = useState<AdGroupSearchItem[]>([]);
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
    mutationFn: async (groups: AdGroupSearchItem[]) => {
      const results = await runSequentialMembershipAdd(groups, (group) =>
        addAdUserToGroup(userId!, { groupDistinguishedName: group.distinguishedName }),
      );
      return partitionSequentialAddResults(results);
    },
    onSuccess: async ({ results, succeeded, failed }) => {
      notifySequentialAddResults({
        t,
        results,
        allSuccessMessageKey: "adManagement:membershipMultiSelect.allGroupsAdded",
        partialSuccessMessageKey: "adManagement:membershipMultiSelect.partialSuccess",
        allFailedMessageKey: "adManagement:users.groups.messages.operationFailed",
        getDefaultErrorMessage: () => t("adManagement:users.groups.messages.operationFailed"),
      });

      setSelectedGroups((current) =>
        current.filter((group) =>
          failed.some((item) => item.distinguishedName === group.distinguishedName),
        ),
      );

      if (succeeded.length > 0) {
        await invalidateAdUserGroupsQuery(queryClient, userId!);
      }
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
        toast.error(t("adManagement:users.groups.messages.operationFailed"));
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
    return <Navigate to={AD_USERS_LIST_PATH} replace />;
  }

  const userSummary = groupsQuery.data;

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
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
                  <AdGroupMultiSearchCombobox
                    selectedItems={selectedGroups}
                    onSelectedItemsChange={setSelectedGroups}
                    disabledGroupDns={memberGroupDns}
                    disabled={addMutation.isPending}
                  />
                  <AdMembershipSelectionChips
                    title={t("adManagement:membershipMultiSelect.selectedGroups")}
                    emptyMessage={t("adManagement:membershipMultiSelect.noGroupsSelected")}
                    items={selectedGroups.map((group) => {
                      const primaryLabel = formatAdGroupSelectionPrimaryLabel(group);
                      return {
                        key: group.distinguishedName,
                        primaryLabel,
                        secondaryLabel: formatAdGroupSelectionSecondaryLabel(group),
                        distinguishedName: group.distinguishedName,
                      };
                    })}
                    onRemove={(key) => {
                      setSelectedGroups((current) =>
                        current.filter((group) => group.distinguishedName !== key),
                      );
                    }}
                    disabled={addMutation.isPending}
                    removeAriaLabel={t("adManagement:membershipMultiSelect.removeSelection")}
                  />
                  <Button
                    type="button"
                    disabled={selectedGroups.length === 0 || addMutation.isPending}
                    onClick={() => {
                      if (selectedGroups.length === 0) {
                        return;
                      }

                      addMutation.mutate(selectedGroups);
                    }}
                  >
                    {t("adManagement:membershipMultiSelect.addSelected")}
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
      </section>

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
