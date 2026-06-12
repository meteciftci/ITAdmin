import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DataTable, DataTablePagination } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { AD_COMPUTERS_LIST_DEFAULTS } from "@/features/ad-management/ad-computers-list-query";
import { createAdComputerColumns } from "@/features/ad-management/ad-computers-columns";
import { buildAdComputerDetailPath } from "@/features/ad-management/ad-computer-detail-path";
import { buildAdComputersListReturnState } from "@/features/ad-management/ad-computers-return-path";
import { getAdComputerPrimaryLabel } from "@/features/ad-management/ad-computer-display-labels";
import {
  AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
  disableAdComputer,
  enableAdComputer,
  getAdComputers,
  invalidateAdManagementComputerQueries,
} from "@/features/ad-management/api";
import { AdComputersSearchToolbar } from "@/features/ad-management/components/AdComputersSearchToolbar";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { useAdComputerListState } from "@/features/ad-management/use-ad-computer-list-state";
import type {
  AdComputerAccountConfirmAction,
  AdComputerListItem,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

const MIN_SEARCH_LENGTH = 2;

type AccountConfirmTarget = {
  computer: AdComputerListItem;
  action: AdComputerAccountConfirmAction;
};

export function AdComputersPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();
  const canEnableComputer = canAccess(currentUser, "AdManagement.Computers.Enable");
  const canDisableComputer = canAccess(currentUser, "AdManagement.Computers.Disable");
  const navigate = useNavigate();
  const { listState, listPath, updateListState, clearListState } = useAdComputerListState();
  const [confirmTarget, setConfirmTarget] = useState<AccountConfirmTarget | null>(null);

  const normalizedSearch = listState.search.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;
  const effectiveSearch = canSearch ? normalizedSearch : undefined;
  const activeFilterCount =
    (listState.status !== AD_COMPUTERS_LIST_DEFAULTS.status ? 1 : 0)
    + (listState.operatingSystem.trim() ? 1 : 0);

  const computersQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
      "list",
      effectiveSearch,
      listState.status,
      listState.operatingSystem,
      listState.pageNumber,
      listState.pageSize,
    ],
    queryFn: () =>
      getAdComputers({
        search: effectiveSearch,
        status: listState.status,
        operatingSystem: listState.operatingSystem.trim() || undefined,
        pageNumber: listState.pageNumber,
        pageSize: listState.pageSize,
      }),
    enabled: moduleStatus.isOperational && canSearch,
  });

  const computers = useMemo(() => computersQuery.data?.items ?? [], [computersQuery.data]);

  const columns = useMemo(
    () =>
      createAdComputerColumns({
        t,
        canDisableComputer,
        canEnableComputer,
        onDetail: (computer) => {
          navigate(buildAdComputerDetailPath(computer.id), {
            state: buildAdComputersListReturnState(),
          });
        },
        onDisable: (computer) => setConfirmTarget({ computer, action: "disable" }),
        onEnable: (computer) => setConfirmTarget({ computer, action: "enable" }),
      }),
    [t, canDisableComputer, canEnableComputer, navigate],
  );

  const table = useServerDataTable({
    data: computers,
    columns,
    pageCount: computersQuery.data?.hasNextPage
      ? listState.pageNumber + 1
      : listState.pageNumber,
    pageIndex: listState.pageNumber - 1,
    pageSize: listState.pageSize,
  });

  const accountOperationMutation = useMutation({
    mutationFn: async ({
      computerId,
      action,
    }: {
      computerId: string;
      action: AdComputerAccountConfirmAction;
    }) => {
      if (action === "enable") {
        return enableAdComputer(computerId);
      }

      return disableAdComputer(computerId);
    },
    onSuccess: async (response, variables) => {
      if (!response.success) {
        toast.error(
          variables.action === "enable"
            ? t("adManagement:computers.messages.enableFailed")
            : t("adManagement:computers.messages.disableFailed"),
        );
        return;
      }

      await invalidateAdManagementComputerQueries(queryClient);

      const message =
        variables.action === "enable"
          ? t("adManagement:computers.messages.enabled")
          : t("adManagement:computers.messages.disabled");
      toast.success(response.message || message);
      setConfirmTarget(null);
    },
    onError: (error, variables) => {
      toast.error(
        getApiErrorMessage(
          error,
          variables.action === "enable"
            ? t("adManagement:computers.messages.enableFailed")
            : t("adManagement:computers.messages.disableFailed"),
        ),
      );
    },
  });

  const confirmCopy = useMemo(() => {
    if (!confirmTarget) {
      return { title: "", description: "", variant: "default" as const };
    }

    const computerLabel = getAdComputerPrimaryLabel(confirmTarget.computer);

    if (confirmTarget.action === "disable") {
      return {
        title: t("adManagement:computers.confirm.disableTitle"),
        description: t("adManagement:computers.confirm.disableDescription", {
          name: computerLabel,
        }),
        variant: "danger" as const,
      };
    }

    return {
      title: t("adManagement:computers.confirm.enableTitle"),
      description: t("adManagement:computers.confirm.enableDescription", {
        name: computerLabel,
      }),
      variant: "default" as const,
    };
  }, [confirmTarget, t]);

  const handleRefresh = () => {
    if (!canSearch) {
      return;
    }

    computersQuery.refetch();
  };

  if (moduleStatus.isOperational && computersQuery.isError) {
    const routeState = createApiErrorRouteState(computersQuery.error, {
      fromPath: listPath,
      retryPath: listPath,
      sourceLabel: t("adManagement:computers.title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <AdManagementModuleStateGuard>
      <section className="space-y-4">
        <SectionCard
          title={t("adManagement:computers.title")}
          description={t("adManagement:computers.description")}
        >
          <div className="space-y-4">
            <AdComputersSearchToolbar
              listState={listState}
              canSearch={canSearch}
              activeFilterCount={activeFilterCount}
              onListStateChange={updateListState}
              onClearFilters={clearListState}
              onRefresh={handleRefresh}
            />

            {!canSearch ? (
              <EmptyState
                title={t("adManagement:computers.empty.searchRequiredTitle")}
                description={t("adManagement:computers.empty.searchRequired")}
              />
            ) : null}

            {canSearch && computersQuery.isLoading ? <LoadingState /> : null}

            {canSearch && computersQuery.isSuccess && !computers.length ? (
              <EmptyState
                title={t("adManagement:computers.empty.title")}
                description={t("adManagement:computers.empty.description")}
              />
            ) : null}

            {canSearch && computers.length > 0 ? (
              <DataTable
                table={table}
                footer={
                  computersQuery.data ? (
                    <DataTablePagination
                      mode="directory"
                      pageNumber={computersQuery.data.pageNumber}
                      pageSize={computersQuery.data.pageSize}
                      hasNextPage={computersQuery.data.hasNextPage}
                      onPageChange={(nextPage) => {
                        updateListState({ pageNumber: nextPage });
                      }}
                      onPageSizeChange={(nextPageSize) => {
                        updateListState({
                          pageSize: nextPageSize,
                          pageNumber: AD_COMPUTERS_LIST_DEFAULTS.pageNumber,
                        });
                      }}
                    />
                  ) : null
                }
              />
            ) : null}
          </div>
        </SectionCard>
      </section>

      <ConfirmDialog
        open={confirmTarget !== null}
        onOpenChange={(open) => {
          if (!open) {
            setConfirmTarget(null);
          }
        }}
        title={confirmCopy.title}
        description={confirmCopy.description}
        confirmText={t("common:actions.confirm")}
        cancelText={t("common:actions.cancel")}
        variant={confirmCopy.variant}
        isLoading={accountOperationMutation.isPending}
        onConfirm={() => {
          if (confirmTarget) {
            accountOperationMutation.mutate({
              computerId: confirmTarget.computer.id,
              action: confirmTarget.action,
            });
          }
        }}
      />
    </AdManagementModuleStateGuard>
  );
}
