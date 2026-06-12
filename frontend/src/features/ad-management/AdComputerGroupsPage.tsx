import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate, useLocation, useParams } from "react-router-dom";
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
import { buttonVariants } from "@/components/ui/button-variants";
import { Button } from "@/components/ui/button";
import { isAdComputerAccountOperationRestricted } from "@/features/ad-management/ad-computer-account-guard";
import { createAdComputerGroupColumns } from "@/features/ad-management/ad-computer-groups-columns";
import { AD_COMPUTERS_LIST_PATH } from "@/features/ad-management/ad-computers-list-path";
import { resolveAdComputerReturnPath } from "@/features/ad-management/ad-computers-return-path";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import {
  addAdComputerToGroup,
  AD_MANAGEMENT_COMPUTER_GROUPS_QUERY_KEY,
  AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
  getAdComputerById,
  getAdComputerGroups,
  invalidateAdComputerGroupsQuery,
  removeAdComputerFromGroup,
} from "@/features/ad-management/api";
import {
  filterAdUserGroupMemberships,
  formatAdGroupSelectionPrimaryLabel,
  formatAdGroupSelectionSecondaryLabel,
} from "@/features/ad-management/ad-group-display";
import { AdComputerGroupMultiSearchCombobox } from "@/features/ad-management/components/AdComputerGroupMultiSearchCombobox";
import { AdMembershipSelectionChips } from "@/features/ad-management/components/AdMembershipSelectionChips";
import {
  notifySequentialAddResults,
  partitionSequentialAddResults,
  runSequentialMembershipAdd,
} from "@/features/ad-management/run-sequential-membership-add";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type {
  AdComputerGroupCandidateItem,
  AdComputerGroupMembershipItem,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

const DEFAULT_MEMBERSHIP_PAGE_SIZE = 10;
const MEMBERSHIP_PAGE_SIZE_OPTIONS = [10, 25, 50] as const;

export function AdComputerGroupsPage() {
  const { t } = useTranslation(["adManagement", "common"]);
  const { id: computerId } = useParams<{ id: string }>();
  const location = useLocation();
  const queryClient = useQueryClient();
  const returnPath = resolveAdComputerReturnPath(location.state, AD_COMPUTERS_LIST_PATH);
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();
  const hasValidId = Boolean(computerId?.trim()) && isGuidLike(computerId);

  const canAddGroup = canAccess(currentUser, "AdManagement.Computers.Groups.Add");
  const canRemoveGroup = canAccess(currentUser, "AdManagement.Computers.Groups.Remove");

  const [selectedGroups, setSelectedGroups] = useState<AdComputerGroupCandidateItem[]>([]);
  const [removeTarget, setRemoveTarget] = useState<AdComputerGroupMembershipItem | null>(null);
  const [membershipSearch, setMembershipSearch] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_MEMBERSHIP_PAGE_SIZE);

  const computerQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_COMPUTERS_QUERY_KEY, "detail", computerId],
    queryFn: () => getAdComputerById(computerId!),
    enabled: hasValidId && moduleStatus.isOperational,
  });

  const groupsQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_COMPUTER_GROUPS_QUERY_KEY, computerId],
    queryFn: () => getAdComputerGroups(computerId!),
    enabled: hasValidId && moduleStatus.isOperational,
  });

  const isProtected = computerQuery.data
    ? isAdComputerAccountOperationRestricted(computerQuery.data)
    : false;
  const showAddGroup = canAddGroup && !isProtected;
  const showRemoveGroup = canRemoveGroup && !isProtected;

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
    mutationFn: async (groups: AdComputerGroupCandidateItem[]) => {
      const results = await runSequentialMembershipAdd(groups, (group) =>
        addAdComputerToGroup(computerId!, { groupDistinguishedName: group.distinguishedName }),
      );
      return partitionSequentialAddResults(results);
    },
    onSuccess: async ({ results, succeeded, failed }) => {
      notifySequentialAddResults({
        t,
        results,
        allSuccessMessageKey: "adManagement:membershipMultiSelect.allGroupsAdded",
        partialSuccessMessageKey: "adManagement:membershipMultiSelect.partialSuccess",
        allFailedMessageKey: "adManagement:computers.groups.messages.addFailed",
        getDefaultErrorMessage: () => t("adManagement:computers.groups.messages.addFailed"),
      });

      setSelectedGroups((current) =>
        current.filter((group) =>
          failed.some((item) => item.distinguishedName === group.distinguishedName),
        ),
      );

      if (succeeded.length > 0) {
        await invalidateAdComputerGroupsQuery(queryClient, computerId!);
      }
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:computers.groups.messages.addFailed")),
      );
    },
  });

  const removeMutation = useMutation({
    mutationFn: (groupDistinguishedName: string) =>
      removeAdComputerFromGroup(computerId!, { groupDistinguishedName }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(t("adManagement:computers.groups.messages.removeFailed"));
        return;
      }

      toast.success(
        response.message || t("adManagement:computers.groups.messages.membershipRemoved"),
      );
      setRemoveTarget(null);
      await invalidateAdComputerGroupsQuery(queryClient, computerId!);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:computers.groups.messages.removeFailed")),
      );
    },
  });

  const columns = useMemo(
    () =>
      createAdComputerGroupColumns({
        t,
        canRemoveGroup: showRemoveGroup,
        isRemovePending: removeMutation.isPending,
        onRemove: setRemoveTarget,
      }),
    [t, showRemoveGroup, removeMutation.isPending],
  );

  const table = useServerDataTable({
    data: paginatedGroups,
    columns,
    pageCount: totalPages,
    pageIndex: safePageNumber - 1,
    pageSize,
  });

  if (!hasValidId) {
    return <Navigate to={AD_COMPUTERS_LIST_PATH} replace />;
  }

  const computerSummary = groupsQuery.data;
  const removeGroupLabel = removeTarget
    ? formatAdGroupSelectionPrimaryLabel(removeTarget)
    : "";
  const removeComputerLabel =
    computerSummary?.name
    ?? computerSummary?.samAccountName
    ?? computerId;

  return (
    <AdManagementModuleStateGuard>
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <PageHeader
          title={t("adManagement:computers.groups.pageTitle")}
          description={t("adManagement:computers.groups.pageDescription")}
          actions={
            <Link
              to={returnPath}
              className={cn(buttonVariants({ variant: "outline" }))}
            >
              {t("common:actions.back")}
            </Link>
          }
        />

        {isProtected ? (
          <p className="rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-muted-foreground">
            {t("adManagement:computers.groups.protected")}
          </p>
        ) : null}

        {groupsQuery.isLoading || computerQuery.isLoading ? <LoadingState /> : null}

        {groupsQuery.isError ? (
          <ErrorState
            title={t("adManagement:computers.groups.errors.loadFailed")}
            description={getApiErrorMessage(
              groupsQuery.error,
              t("adManagement:computers.groups.errors.loadFailed"),
            )}
          />
        ) : null}

        {groupsQuery.isSuccess && computerSummary ? (
          <>
            <SectionCard
              title={t("adManagement:computers.groups.sections.computerSummary")}
              description={t("adManagement:computers.groups.pageHeading")}
            >
              <div className="grid gap-4 md:grid-cols-2">
                <SummaryField
                  label={t("adManagement:computers.moveOu.fields.name")}
                  value={computerSummary.name}
                />
                <SummaryField
                  label={t("adManagement:computers.table.samAccountName")}
                  value={computerSummary.samAccountName}
                />
                <SummaryField
                  label={t("adManagement:computers.table.dnsHostName")}
                  value={computerSummary.dnsHostName}
                />
                <SummaryField
                  label={t("adManagement:computers.groups.table.distinguishedName")}
                  value={computerSummary.distinguishedName}
                  mono
                />
              </div>
            </SectionCard>

            {showAddGroup ? (
              <SectionCard
                title={t("adManagement:computers.groups.sections.addGroup")}
                description={t("adManagement:computers.groups.sections.addGroupDescription")}
              >
                <div className="space-y-4">
                  <AdComputerGroupMultiSearchCombobox
                    computerId={computerId!}
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
              title={t("adManagement:computers.groups.sections.currentMemberships")}
              description={t("adManagement:computers.groups.sections.currentMembershipsDescription")}
            >
              {allGroups.length === 0 ? (
                <EmptyState
                  title={t("adManagement:computers.groups.empty.noMembershipsTitle")}
                  description={t("adManagement:computers.groups.empty.noMembershipsDescription")}
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
                      "adManagement:computers.groups.fields.membershipSearchPlaceholder",
                    )}
                    showFiltersButton={false}
                    toolbarFooter={
                      totalCount > 0 ? (
                        <span className="text-sm text-muted-foreground">
                          {t("adManagement:computers.groups.pagination.rangeInfo", {
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
                      title={t("adManagement:computers.groups.empty.searchNoResultsTitle")}
                      description={t(
                        "adManagement:computers.groups.empty.searchNoResultsDescription",
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
        title={t("adManagement:computers.groups.confirm.removeTitle")}
        description={t("adManagement:computers.groups.confirm.removeDescription", {
          computer: removeComputerLabel,
          group: removeGroupLabel,
        })}
        confirmText={t("adManagement:computers.groups.confirm.removeConfirm")}
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
