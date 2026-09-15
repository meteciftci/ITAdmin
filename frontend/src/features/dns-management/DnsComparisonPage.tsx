import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { SwitchField } from "@/components/common/SwitchField";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { DateTimeText } from "@/components/common/DateTimeText";
import { FormError } from "@/components/common/FormError";
import { LoadingState } from "@/components/common/LoadingState";
import { MultiSelectFilter } from "@/components/common/MultiSelectFilter";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/features/auth/auth-store";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { getApiErrorMessage } from "@/lib/api-error";
import { PermissionCodes } from "@/lib/permission-codes";
import { canAccess } from "@/lib/permissions";
import {
  compareDnsInventory,
  DNS_COMPARISON_CONTEXT_QUERY_KEY,
  DNS_COMPARISON_RESULTS_QUERY_KEY,
  DNS_COMPARISON_ZONES_QUERY_KEY,
  exportDnsComparison,
  getDnsComparisonContext,
  getDnsComparisonZones,
  synchronizeAllDnsInventory,
} from "./api";
import { createDnsComparisonColumns } from "./dns-comparison-columns";
import { saveDnsDownload } from "./download-dns-export";
import type { DnsComparisonRequest } from "./types";

const promptSessionKey = "itadmin:dns-comparison-full-sync-prompt-shown";

export function DnsComparisonPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const user = useAuthStore((state) => state.user);
  const canSynchronize = canAccess(
    user,
    PermissionCodes.DnsManagement.Synchronize,
  );
  const canExport = canAccess(user, PermissionCodes.DnsManagement.Export);
  const queryClient = useQueryClient();
  const [selectedServerNames, setSelectedServerNames] = useState<string[]>([]);
  const [selectedZones, setSelectedZones] = useState<string[]>([]);
  const [compareTimeToLive, setCompareTimeToLive] = useState(false);
  const [appliedSelection, setAppliedSelection] = useState<Omit<
    DnsComparisonRequest,
    "search" | "pageNumber" | "pageSize"
  > | null>(null);
  const [search, setSearch] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [syncPromptDismissed, setSyncPromptDismissed] = useState(
    () => sessionStorage.getItem(promptSessionKey) === "true",
  );
  const effectiveSearch = useDebouncedValue(search, 350).trim() || undefined;
  const synchronizationWasRunning = useRef(false);

  const context = useQuery({
    queryKey: DNS_COMPARISON_CONTEXT_QUERY_KEY,
    queryFn: getDnsComparisonContext,
    refetchInterval: (query) =>
      query.state.data?.synchronizationInProgress ? 2000 : false,
  });
  const availableServers = useMemo(
    () => context.data?.servers.filter((server) => server.isEnabled) ?? [],
    [context.data],
  );
  const selectedServerIds = useMemo(() => {
    const selectedNames = new Set(selectedServerNames);
    return availableServers
      .filter((server) => selectedNames.has(server.serverDisplayName))
      .map((server) => server.serverId);
  }, [availableServers, selectedServerNames]);
  const zones = useQuery({
    queryKey: [...DNS_COMPARISON_ZONES_QUERY_KEY, selectedServerIds],
    queryFn: () => getDnsComparisonZones(selectedServerIds),
    enabled: selectedServerIds.length >= 2,
  });
  const comparison = useQuery({
    queryKey: [
      ...DNS_COMPARISON_RESULTS_QUERY_KEY,
      appliedSelection,
      effectiveSearch,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      compareDnsInventory({
        ...appliedSelection!,
        search: effectiveSearch,
        pageNumber,
        pageSize,
      }),
    enabled: appliedSelection !== null,
  });
  const sync = useMutation({
    mutationFn: synchronizeAllDnsInventory,
    onSuccess: async (result) => {
      dismissSyncPrompt();
      toast.success(
        t("comparison.syncQueued", {
          queued: result.queuedCount,
          existing: result.alreadyQueuedCount,
        }),
      );
      if (result.failedCount > 0) {
        toast.error(`${t("comparison.syncFailed")} (${result.failedCount})`);
      }
      await queryClient.invalidateQueries({
        queryKey: DNS_COMPARISON_CONTEXT_QUERY_KEY,
      });
    },
    onError: (error) =>
      toast.error(getApiErrorMessage(error, t("comparison.syncFailed"))),
  });
  const exportMutation = useMutation({
    mutationFn: () =>
      exportDnsComparison({ ...appliedSelection!, search: effectiveSearch }),
    onSuccess: (download) => {
      saveDnsDownload(download);
      toast.success(t("export.completed"));
    },
    onError: (error) =>
      toast.error(getApiErrorMessage(error, t("export.failed"))),
  });
  const showSyncPrompt = Boolean(
    context.data?.promptForFullSyncOnOpen &&
    canSynchronize &&
    !syncPromptDismissed,
  );
  useEffect(() => {
    const isRunning = context.data?.synchronizationInProgress ?? false;
    if (synchronizationWasRunning.current && !isRunning) {
      void queryClient.invalidateQueries({
        queryKey: DNS_COMPARISON_ZONES_QUERY_KEY,
      });
      void queryClient.invalidateQueries({
        queryKey: DNS_COMPARISON_RESULTS_QUERY_KEY,
      });
    }
    synchronizationWasRunning.current = isRunning;
  }, [context.data?.synchronizationInProgress, queryClient]);
  const displayServers = useMemo(
    () =>
      comparison.data?.servers ??
      availableServers.filter((server) =>
        appliedSelection?.serverIds.includes(server.serverId),
      ),
    [comparison.data?.servers, availableServers, appliedSelection?.serverIds],
  );
  const columns = useMemo(
    () =>
      createDnsComparisonColumns({
        t,
        servers: displayServers,
        compareTimeToLive: appliedSelection?.compareTimeToLive ?? false,
      }),
    [t, displayServers, appliedSelection?.compareTimeToLive],
  );
  const table = useServerDataTable({
    data: comparison.data?.items ?? [],
    columns,
    pageCount: comparison.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  const resetAppliedSelection = () => {
    setAppliedSelection(null);
    setPageNumber(1);
  };
  const handleServerChange = (values: string[]) => {
    setSelectedServerNames(values.slice(0, 10));
    setSelectedZones([]);
    resetAppliedSelection();
  };
  const handleZoneChange = (values: string[]) => {
    setSelectedZones(values.slice(0, 20));
    resetAppliedSelection();
  };
  const runComparison = () => {
    setAppliedSelection({
      serverIds: selectedServerIds,
      zoneNames: selectedZones,
      compareTimeToLive,
    });
    setPageNumber(1);
  };
  function dismissSyncPrompt() {
    sessionStorage.setItem(promptSessionKey, "true");
    setSyncPromptDismissed(true);
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("comparison.title")}
        description={t("comparison.description")}
        actions={
          canSynchronize ? (
            <Button
              variant="outline"
              disabled={
                sync.isPending || context.data?.synchronizationInProgress
              }
              onClick={() => sync.mutate()}
            >
              {sync.isPending
                ? t("comparison.queueingSync")
                : context.data?.synchronizationInProgress
                  ? t("comparison.syncInProgress")
                  : t("comparison.syncAll")}
            </Button>
          ) : undefined
        }
      />
      {context.isLoading ? <LoadingState /> : null}
      {context.isError ? (
        <FormError
          message={getApiErrorMessage(
            context.error,
            t("comparison.loadFailed"),
          )}
        />
      ) : null}

      {context.data ? (
        <SectionCard
          title={t("comparison.freshnessTitle")}
          description={t("comparison.freshnessDescription")}
        >
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-lg border p-4">
              <p className="text-xs text-muted-foreground">
                {t("comparison.lastFullSync")}
              </p>
              <p className="mt-1 font-medium">
                {context.data.lastFullInventorySyncAt ? (
                  <DateTimeText value={context.data.lastFullInventorySyncAt} />
                ) : (
                  t("comparison.fullSyncUnavailable")
                )}
              </p>
            </div>
            <div className="rounded-lg border p-4">
              <p className="text-xs text-muted-foreground">
                {t("comparison.enabledServers")}
              </p>
              <p className="mt-1 text-xl font-semibold">
                {context.data.enabledServerCount}
              </p>
            </div>
            <div className="rounded-lg border p-4">
              <p className="text-xs text-muted-foreground">
                {t("comparison.unavailableServers")}
              </p>
              <div className="mt-1">
                <Badge
                  variant={
                    context.data.unavailableServerCount
                      ? "destructive"
                      : "default"
                  }
                >
                  {context.data.unavailableServerCount}
                </Badge>
              </div>
            </div>
            <div className="rounded-lg border p-4">
              <p className="text-xs text-muted-foreground">
                {t("synchronization.inventory")}
              </p>
              <div className="mt-1">
                <Badge
                  variant={
                    context.data.synchronizationInProgress
                      ? "secondary"
                      : "default"
                  }
                >
                  {t(
                    context.data.synchronizationInProgress
                      ? "synchronization.status.Running"
                      : "synchronization.status.Completed",
                  )}
                </Badge>
              </div>
            </div>
          </div>
          <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {context.data.servers
              .filter((server) => server.isEnabled)
              .map((server) => {
                const hasFullInventory =
                  server.isAvailable &&
                  server.snapshotScope === "FullInventory";
                const statusKey = !hasFullInventory
                  ? "inventory.unavailable"
                  : server.isStale
                    ? "inventory.stale"
                    : "inventory.current";
                return (
                  <div key={server.serverId} className="rounded-lg border p-3">
                    <div className="flex items-start justify-between gap-2">
                      <span className="text-sm font-medium">
                        {server.serverDisplayName}
                      </span>
                      <Badge
                        variant={
                          !hasFullInventory
                            ? "destructive"
                            : server.isStale
                              ? "secondary"
                              : "default"
                        }
                      >
                        {t(statusKey)}
                      </Badge>
                    </div>
                    <p className="mt-2 text-xs text-muted-foreground">
                      {server.snapshotCompletedAt ? (
                        <DateTimeText value={server.snapshotCompletedAt} />
                      ) : (
                        t("comparison.fullSyncUnavailable")
                      )}
                    </p>
                  </div>
                );
              })}
          </div>
        </SectionCard>
      ) : null}

      <SectionCard
        title={t("comparison.selectionTitle")}
        description={t("comparison.selectionDescription")}
      >
        <div className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-2">
            <div className="space-y-2">
              <p className="text-sm font-medium">
                {t("comparison.selectServers")}
              </p>
              <MultiSelectFilter
                label={t("comparison.selectServers")}
                placeholder={t("comparison.serverPlaceholder")}
                options={availableServers.map(
                  (server) => server.serverDisplayName,
                )}
                selectedValues={selectedServerNames}
                onChange={handleServerChange}
                clearLabel={t("common:select.clearSelection")}
                emptyLabel={t("common:select.noOptions")}
                searchPlaceholder={t("common:select.searchOptions")}
              />
              <p className="text-xs text-muted-foreground">
                {t("comparison.serverSelectionHelp")}
              </p>
            </div>
            <div className="space-y-2">
              <p className="text-sm font-medium">
                {t("comparison.selectZones")}
              </p>
              <MultiSelectFilter
                label={t("comparison.selectZones")}
                placeholder={
                  selectedServerIds.length < 2
                    ? t("comparison.selectServersFirst")
                    : t("comparison.zonePlaceholder")
                }
                options={zones.data?.map((zone) => zone.name) ?? []}
                selectedValues={selectedZones}
                onChange={handleZoneChange}
                clearLabel={t("common:select.clearSelection")}
                emptyLabel={
                  zones.isLoading
                    ? t("comparison.loadingZones")
                    : t("common:select.noOptions")
                }
                searchPlaceholder={t("common:select.searchOptions")}
                disabled={selectedServerIds.length < 2}
              />
              <p className="text-xs text-muted-foreground">
                {t("comparison.zoneSelectionHelp")}
              </p>
            </div>
          </div>
          {zones.isError ? (
            <FormError
              message={getApiErrorMessage(
                zones.error,
                t("comparison.loadZonesFailed"),
              )}
            />
          ) : null}
          <SwitchField
            id="dns-compare-ttl"
            label={t("comparison.compareTtl")}
            checked={compareTimeToLive}
            onCheckedChange={(value) => {
              setCompareTimeToLive(value === true);
              resetAppliedSelection();
            }}
          />
          <div className="flex justify-end">
            <Button
              disabled={
                selectedServerIds.length < 2 || selectedZones.length === 0
              }
              onClick={runComparison}
            >
              {t("comparison.showReport")}
            </Button>
          </div>
        </div>
      </SectionCard>

      <SectionCard
        title={t("comparison.resultTitle")}
        description={t("comparison.resultDescription")}
        actions={
          canExport && appliedSelection ? (
            <Button
              variant="outline"
              disabled={exportMutation.isPending}
              onClick={() => exportMutation.mutate()}
            >
              {exportMutation.isPending
                ? t("export.preparing")
                : t("export.csv")}
            </Button>
          ) : undefined
        }
      >
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("comparison.searchRecords")}
          />
          <DataTable
            table={table}
            isLoading={comparison.isLoading}
            emptyMessage={
              appliedSelection
                ? t("comparison.empty")
                : t("comparison.notStarted")
            }
            emptyDescription={
              appliedSelection
                ? t("comparison.emptyDescription")
                : t("comparison.notStartedDescription")
            }
            footer={
              comparison.data ? (
                <DataTablePagination
                  mode="server"
                  pageNumber={comparison.data.pageNumber}
                  pageSize={comparison.data.pageSize}
                  totalCount={comparison.data.totalCount}
                  totalPages={comparison.data.totalPages}
                  onPageChange={setPageNumber}
                  onPageSizeChange={(value) => {
                    setPageSize(value);
                    setPageNumber(1);
                  }}
                />
              ) : null
            }
          />
          {comparison.isError ? (
            <FormError
              message={getApiErrorMessage(
                comparison.error,
                t("comparison.loadFailed"),
              )}
            />
          ) : null}
        </div>
      </SectionCard>

      <ConfirmDialog
        open={showSyncPrompt}
        title={t("comparison.promptTitle")}
        description={t("comparison.promptDescription")}
        confirmText={t("comparison.refreshNow")}
        cancelText={t("comparison.continueCached")}
        isLoading={sync.isPending}
        onOpenChange={(open) => {
          if (!open) dismissSyncPrompt();
        }}
        onConfirm={() => sync.mutate()}
      />
    </section>
  );
}
