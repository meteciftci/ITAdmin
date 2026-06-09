import { forwardRef, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
  type DataTableColumnMeta,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import {
  getAdGroupMemberPrimaryLabel,
  getAdGroupMemberSecondaryLabel,
} from "@/features/ad-management/ad-group-display-labels";
import { getAdGroupMemberTypeLabel } from "@/features/ad-management/ad-group-labels";
import {
  AD_MANAGEMENT_GROUP_MEMBERS_QUERY_KEY,
  getAdGroupMembers,
  invalidateAdGroupMemberQueries,
  removeAdGroupMember,
} from "@/features/ad-management/api";
import { AdAddGroupMemberDialog } from "@/features/ad-management/components/AdAddGroupMemberDialog";
import { AdRemoveGroupMemberConfirmDialog } from "@/features/ad-management/components/AdRemoveGroupMemberConfirmDialog";
import type {
  AdGroupMemberListItem,
  AdGroupMemberListTypeFilter,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";

const DEFAULT_PAGE_SIZE = 20;
const PAGE_SIZE_OPTIONS = [10, 20, 50] as const;

type Props = {
  groupId: string;
  groupName: string | null;
  memberCount: number;
  canManageMembers: boolean;
  enabled: boolean;
};

export const AdGroupMembersSection = forwardRef<HTMLDivElement, Props>(function AdGroupMembersSection(
  {
    groupId,
    groupName,
    memberCount,
    canManageMembers,
    enabled,
  },
  ref,
) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState<AdGroupMemberListTypeFilter>("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<AdGroupMemberListItem | null>(null);

  const membersQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_GROUP_MEMBERS_QUERY_KEY,
      groupId,
      search,
      typeFilter,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getAdGroupMembers(groupId, {
        search: search || undefined,
        type: typeFilter,
        pageNumber,
        pageSize,
      }),
    enabled,
    staleTime: 0,
  });

  const removeMutation = useMutation({
    mutationFn: (memberDistinguishedName: string) =>
      removeAdGroupMember(groupId, { memberDistinguishedName }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(
          response.message || t("adManagement:groups.members.removeError"),
        );
        return;
      }

      toast.success(
        response.message || t("adManagement:groups.members.removeSuccess"),
      );
      setRemoveTarget(null);
      await invalidateAdGroupMemberQueries(queryClient, groupId);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:groups.members.removeError")),
      );
    },
  });

  const items = membersQuery.data?.items ?? [];
  const totalCount = membersQuery.data?.memberCount ?? memberCount;
  const hasNextPage = membersQuery.data?.hasNextPage ?? false;
  const totalPages = hasNextPage ? pageNumber + 1 : pageNumber;
  const rangeStart = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const rangeEnd = totalCount === 0 ? 0 : Math.min(pageNumber * pageSize, totalCount);
  const hasActiveFilters = search.trim().length > 0 || typeFilter !== "all";
  const memberCountText = t("adManagement:groups.detail.memberCount", { count: totalCount });
  const purposeText = canManageMembers
    ? t("adManagement:groups.members.descriptionManage")
    : t("adManagement:groups.members.descriptionView");
  const sectionDescription = `${memberCountText} · ${purposeText}`;

  const columns = useMemo((): ColumnDef<AdGroupMemberListItem, unknown>[] => {
    const memberColumns: ColumnDef<AdGroupMemberListItem, unknown>[] = [
      {
        id: "member",
        header: () => t("adManagement:groups.members.memberColumn"),
        cell: ({ row }) => {
          const member = row.original;
          const primaryLabel = getAdGroupMemberPrimaryLabel(member);
          const secondaryLabel = getAdGroupMemberSecondaryLabel(member, primaryLabel);

          return (
            <div className="space-y-1" title={member.distinguishedName}>
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-medium">{primaryLabel}</p>
                <Badge variant="outline">{getAdGroupMemberTypeLabel(t, member.type)}</Badge>
              </div>
              {secondaryLabel ? (
                <p className="truncate text-xs text-muted-foreground" title={secondaryLabel}>
                  {secondaryLabel}
                </p>
              ) : null}
              {member.description ? (
                <p
                  className="line-clamp-2 text-xs text-muted-foreground"
                  title={member.description}
                >
                  {member.description}
                </p>
              ) : null}
            </div>
          );
        },
      },
    ];

    if (canManageMembers) {
      memberColumns.push({
        id: "actions",
        header: () => t("adManagement:groups.members.actionsColumn"),
        meta: { isAction: true, align: "right" } satisfies DataTableColumnMeta,
        cell: ({ row }) => (
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="text-destructive hover:bg-destructive/10 hover:text-destructive"
            disabled={removeMutation.isPending}
            onClick={() => setRemoveTarget(row.original)}
          >
            {t("adManagement:groups.members.remove")}
          </Button>
        ),
      });
    }

    return memberColumns;
  }, [canManageMembers, removeMutation.isPending, t]);

  const table = useServerDataTable({
    data: items,
    columns,
    pageCount: totalPages,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  return (
    <div ref={ref} tabIndex={-1} className="scroll-mt-4 outline-none">
      <SectionCard
        title={t("adManagement:groups.detail.membersTitle")}
        description={sectionDescription}
        actions={
          canManageMembers ? (
            <Button type="button" size="sm" onClick={() => setAddDialogOpen(true)}>
              {t("adManagement:groups.members.add")}
            </Button>
          ) : undefined
        }
      >
        <div className="space-y-4">
          <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div className="min-w-0 flex-1">
              <DataTableToolbar
                searchValue={search}
                onSearchChange={(value) => {
                  setSearch(value);
                  setPageNumber(1);
                }}
                searchPlaceholder={t("adManagement:groups.members.searchPlaceholder")}
                showFiltersButton={false}
              />
            </div>
            <div className="w-full shrink-0 space-y-2 md:w-44">
              <Label htmlFor="group-members-type-filter">
                {t("adManagement:groups.members.typeFilter")}
              </Label>
              <Select
                id="group-members-type-filter"
                value={typeFilter}
                onChange={(event) => {
                  setTypeFilter(event.target.value as AdGroupMemberListTypeFilter);
                  setPageNumber(1);
                }}
              >
                <option value="all">{t("adManagement:groups.members.types.all")}</option>
                <option value="user">{t("adManagement:groups.members.types.user")}</option>
                <option value="group">{t("adManagement:groups.members.types.group")}</option>
                <option value="computer">{t("adManagement:groups.members.types.computer")}</option>
              </Select>
            </div>
          </div>

          {membersQuery.isLoading ? <LoadingState /> : null}

          {membersQuery.isError ? (
            <ErrorState
              title={t("errors:generic.title")}
              description={getApiErrorMessage(
                membersQuery.error,
                t("errors:generic.description"),
              )}
            />
          ) : null}

          {membersQuery.isSuccess ? (
            items.length === 0 ? (
              <EmptyState
                title={
                  hasActiveFilters
                    ? t("adManagement:groups.members.searchNoResults")
                    : t("adManagement:groups.members.noMembers")
                }
              />
            ) : (
              <div className="space-y-4">
                {totalCount > 0 ? (
                  <span className="text-sm text-muted-foreground">
                    {t("adManagement:groups.members.rangeInfo", {
                      start: rangeStart,
                      end: rangeEnd,
                      total: totalCount,
                    })}
                  </span>
                ) : null}
                <DataTable
                  table={table}
                  footer={
                    totalCount > 0 ? (
                      <DataTablePagination
                        mode="directory"
                        pageNumber={pageNumber}
                        pageSize={pageSize}
                        hasNextPage={hasNextPage}
                        onPageChange={setPageNumber}
                        onPageSizeChange={(nextSize) => {
                          setPageSize(nextSize);
                          setPageNumber(1);
                        }}
                        pageSizeOptions={[...PAGE_SIZE_OPTIONS]}
                      />
                    ) : null
                  }
                />
              </div>
            )
          ) : null}
        </div>
      </SectionCard>

      {canManageMembers ? (
        <>
          <AdAddGroupMemberDialog
            open={addDialogOpen}
            groupId={groupId}
            onOpenChange={setAddDialogOpen}
          />
          <AdRemoveGroupMemberConfirmDialog
            open={removeTarget !== null}
            groupName={groupName}
            member={removeTarget}
            isLoading={removeMutation.isPending}
            onOpenChange={(open) => {
              if (!open) {
                setRemoveTarget(null);
              }
            }}
            onConfirm={() => {
              if (!removeTarget) {
                return;
              }
              removeMutation.mutate(removeTarget.distinguishedName);
            }}
          />
        </>
      ) : null}
    </div>
  );
});
