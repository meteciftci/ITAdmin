import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { SwitchField } from "@/components/common/SwitchField";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { FormError } from "@/components/common/FormError";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { getApiErrorMessage } from "@/lib/api-error";
import {
  DNS_SCAVENGING_CONFIGURATION_QUERY_KEY,
  DNS_SERVERS_QUERY_KEY,
  getDnsScavengingConfiguration,
  getDnsServers,
  mutateDnsScavengingConfiguration,
} from "./api";
import type { DnsScavengingMutationInput, DnsZoneAging } from "./types";

type PendingAction = Omit<DnsScavengingMutationInput, "expectedStateToken">;

export function DnsScavengingPage() {
  const { t, i18n } = useTranslation(["dnsManagement", "common"]);
  const queryClient = useQueryClient();
  const [serverId, setServerId] = useState("");
  const [selectedZoneName, setSelectedZoneName] = useState("");
  const [serverDraft, setServerDraft] = useState<{
    enabled: boolean;
    intervalHours: number;
  } | null>(null);
  const [zoneDraft, setZoneDraft] = useState<{
    name: string;
    enabled: boolean;
    noRefreshHours: number;
    refreshHours: number;
    scavengeServers: string;
  } | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(
    null,
  );
  const [error, setError] = useState<string | null>(null);

  const servers = useQuery({
    queryKey: DNS_SERVERS_QUERY_KEY,
    queryFn: getDnsServers,
  });
  const activeServers = useMemo(
    () => servers.data?.filter((server) => server.isEnabled) ?? [],
    [servers.data],
  );
  const selectedServerId = serverId || activeServers[0]?.id || "";
  const configuration = useQuery({
    queryKey: [...DNS_SCAVENGING_CONFIGURATION_QUERY_KEY, selectedServerId],
    queryFn: () => getDnsScavengingConfiguration(selectedServerId),
    enabled: Boolean(selectedServerId),
  });
  const eligibleZones = useMemo(
    () => configuration.data?.zones.filter((zone) => zone.isEligible) ?? [],
    [configuration.data],
  );
  const selectedZone =
    eligibleZones.find((zone) => zone.name === selectedZoneName) ??
    eligibleZones[0];
  const serverValues = serverDraft ?? {
    enabled: configuration.data?.scavengingEnabled ?? false,
    intervalHours: Math.round(
      (configuration.data?.scavengingIntervalSeconds ?? 604800) / 3600,
    ),
  };
  const zoneValues =
    zoneDraft?.name === selectedZone?.name
      ? zoneDraft
      : selectedZone
        ? {
            name: selectedZone.name,
            enabled: selectedZone.agingEnabled,
            noRefreshHours: Math.round(
              selectedZone.noRefreshIntervalSeconds / 3600,
            ),
            refreshHours: Math.round(
              selectedZone.refreshIntervalSeconds / 3600,
            ),
            scavengeServers: selectedZone.scavengeServers.join("\n"),
          }
        : null;

  const mutation = useMutation({
    mutationFn: (action: PendingAction) =>
      mutateDnsScavengingConfiguration(selectedServerId, {
        ...action,
        expectedStateToken: configuration.data!.stateToken,
      }),
    onSuccess: async (result) => {
      setPendingAction(null);
      setError(null);
      setServerDraft(null);
      setZoneDraft(null);
      if (result.configuration) {
        queryClient.setQueryData(
          [...DNS_SCAVENGING_CONFIGURATION_QUERY_KEY, selectedServerId],
          result.configuration,
        );
      } else {
        await queryClient.invalidateQueries({
          queryKey: [
            ...DNS_SCAVENGING_CONFIGURATION_QUERY_KEY,
            selectedServerId,
          ],
        });
      }
      toast.success(t("scavenging.updated"));
    },
    onError: (failure) => {
      setPendingAction(null);
      setError(getApiErrorMessage(failure, t("scavenging.operationFailed")));
    },
  });

  const changeServer = (value: string) => {
    setServerId(value);
    setSelectedZoneName("");
    setServerDraft(null);
    setZoneDraft(null);
    setError(null);
  };
  const changeZone = (zone: DnsZoneAging) => {
    setSelectedZoneName(zone.name);
    setZoneDraft(null);
  };
  const zoneServerList =
    zoneValues?.scavengeServers
      .split(/[\r\n,;]+/)
      .map((value) => value.trim())
      .filter(Boolean) ?? [];
  const serverDraftValid =
    !serverValues.enabled ||
    (serverValues.intervalHours >= 1 && serverValues.intervalHours <= 8760);
  const zoneDraftValid =
    Boolean(zoneValues) &&
    zoneValues!.noRefreshHours >= 0 &&
    zoneValues!.noRefreshHours <= 8760 &&
    zoneValues!.refreshHours >= 0 &&
    zoneValues!.refreshHours <= 8760 &&
    zoneServerList.length <= 16;
  const canStart = Boolean(
    configuration.data?.scavengingEnabled &&
    eligibleZones.some((zone) => zone.agingEnabled),
  );

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("scavenging.title")}
        description={t("scavenging.description")}
        actions={
          selectedServerId ? (
            <Button
              variant="outline"
              disabled={configuration.isFetching || mutation.isPending}
              onClick={() => {
                setServerDraft(null);
                setZoneDraft(null);
                configuration.refetch();
              }}
            >
              {t("common:actions.refresh")}
            </Button>
          ) : null
        }
      />
      <FormError
        message={
          error ??
          (servers.isError || configuration.isError
            ? t("scavenging.loadFailed")
            : null)
        }
      />

      <SectionCard
        title={t("scavenging.serverTitle")}
        description={t("scavenging.liveNotice")}
      >
        {servers.isLoading ? (
          <LoadingState />
        ) : (
          <div className="max-w-xl space-y-2">
            <Label htmlFor="dns-scavenging-server">
              {t("scavenging.fields.server")}
            </Label>
            <Select
              id="dns-scavenging-server"
              value={selectedServerId}
              disabled={mutation.isPending}
              onChange={(event) => changeServer(event.target.value)}
            >
              <option value="">{t("scavenging.selectServer")}</option>
              {activeServers.map((server) => (
                <option key={server.id} value={server.id}>
                  {server.displayName} ({server.hostName})
                </option>
              ))}
            </Select>
          </div>
        )}
      </SectionCard>

      {configuration.isLoading ? <LoadingState /> : null}
      {configuration.data ? (
        <>
          <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 p-4 text-sm text-foreground">
            <p className="font-medium">{t("scavenging.warningTitle")}</p>
            <p className="mt-1 text-muted-foreground">
              {t("scavenging.warningDescription")}
            </p>
          </div>

          <SectionCard
            title={t("scavenging.scheduleTitle")}
            description={t("scavenging.scheduleDescription")}
          >
            <div className="grid gap-4 md:grid-cols-2">
              <SwitchField
                id="dns-scavenging-enabled"
                label={t("scavenging.fields.enabled")}
                checked={serverValues.enabled}
                disabled={mutation.isPending}
                onCheckedChange={(enabled) =>
                  setServerDraft({ ...serverValues, enabled })
                }
              />
              <div className="space-y-2">
                <Label htmlFor="dns-scavenging-interval">
                  {t("scavenging.fields.interval")}
                </Label>
                <Input
                  id="dns-scavenging-interval"
                  type="number"
                  min="1"
                  max="8760"
                  value={serverValues.intervalHours}
                  disabled={!serverValues.enabled || mutation.isPending}
                  onChange={(event) =>
                    setServerDraft({
                      ...serverValues,
                      intervalHours: Number(event.target.value),
                    })
                  }
                />
              </div>
            </div>
            <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm text-muted-foreground">
                {t("scavenging.lastRun", {
                  value: formatDate(
                    configuration.data.lastScavengeTime,
                    i18n.language,
                  ),
                })}
              </p>
              <Button
                disabled={!serverDraftValid || mutation.isPending}
                onClick={() =>
                  setPendingAction({
                    action: "UpdateServer",
                    scavengingEnabled: serverValues.enabled,
                    scavengingIntervalHours: serverValues.enabled
                      ? serverValues.intervalHours
                      : 0,
                  })
                }
              >
                {t("common:actions.save")}
              </Button>
            </div>
          </SectionCard>

          <SectionCard
            title={t("scavenging.zonesTitle")}
            description={t("scavenging.zonesDescription")}
          >
            <div className="grid gap-5 lg:grid-cols-[minmax(15rem,0.75fr)_minmax(0,1.25fr)]">
              <div className="space-y-2">
                {configuration.data.zones.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    {t("scavenging.noZones")}
                  </p>
                ) : (
                  configuration.data.zones.map((zone) => (
                    <button
                      key={zone.name}
                      type="button"
                      disabled={!zone.isEligible || mutation.isPending}
                      onClick={() => changeZone(zone)}
                      className="flex w-full items-center justify-between gap-3 rounded-md border px-3 py-2 text-left disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      <span className="min-w-0">
                        <span className="block truncate font-mono text-sm">
                          {zone.name}
                        </span>
                        <span className="block text-xs text-muted-foreground">
                          {zone.isEligible
                            ? t("scavenging.primaryZone")
                            : t("scavenging.notEligible")}
                        </span>
                      </span>
                      <Badge
                        variant={zone.agingEnabled ? "success" : "secondary"}
                      >
                        {zone.agingEnabled
                          ? t("scavenging.enabled")
                          : t("scavenging.disabled")}
                      </Badge>
                    </button>
                  ))
                )}
              </div>
              {selectedZone && zoneValues ? (
                <div className="space-y-4 rounded-lg border p-4">
                  <div>
                    <p className="font-mono font-medium">{selectedZone.name}</p>
                    <p className="text-xs text-muted-foreground">
                      {t("scavenging.availableAt", {
                        value: formatDate(
                          selectedZone.availableForScavengeTime,
                          i18n.language,
                        ),
                      })}
                    </p>
                  </div>
                  <SwitchField
                    id="dns-zone-aging-enabled"
                    label={t("scavenging.fields.zoneAgingEnabled")}
                    checked={zoneValues.enabled}
                    disabled={mutation.isPending}
                    onCheckedChange={(enabled) =>
                      setZoneDraft({ ...zoneValues, enabled })
                    }
                  />
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div className="space-y-2">
                      <Label htmlFor="dns-no-refresh">
                        {t("scavenging.fields.noRefresh")}
                      </Label>
                      <Input
                        id="dns-no-refresh"
                        type="number"
                        min="0"
                        max="8760"
                        value={zoneValues.noRefreshHours}
                        disabled={mutation.isPending}
                        onChange={(event) =>
                          setZoneDraft({
                            ...zoneValues,
                            noRefreshHours: Number(event.target.value),
                          })
                        }
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="dns-refresh">
                        {t("scavenging.fields.refresh")}
                      </Label>
                      <Input
                        id="dns-refresh"
                        type="number"
                        min="0"
                        max="8760"
                        value={zoneValues.refreshHours}
                        disabled={mutation.isPending}
                        onChange={(event) =>
                          setZoneDraft({
                            ...zoneValues,
                            refreshHours: Number(event.target.value),
                          })
                        }
                      />
                    </div>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="dns-scavenge-servers">
                      {t("scavenging.fields.scavengeServers")}
                    </Label>
                    <Textarea
                      id="dns-scavenge-servers"
                      rows={3}
                      value={zoneValues.scavengeServers}
                      disabled={mutation.isPending}
                      placeholder={t("scavenging.scavengeServersPlaceholder")}
                      onChange={(event) =>
                        setZoneDraft({
                          ...zoneValues,
                          scavengeServers: event.target.value,
                        })
                      }
                    />
                    <p className="text-xs text-muted-foreground">
                      {t("scavenging.scavengeServersHelp")}
                    </p>
                  </div>
                  <div className="flex justify-end">
                    <Button
                      disabled={!zoneDraftValid || mutation.isPending}
                      onClick={() =>
                        setPendingAction({
                          action: "UpdateZone",
                          zoneName: selectedZone.name,
                          zoneAgingEnabled: zoneValues.enabled,
                          noRefreshIntervalHours: zoneValues.noRefreshHours,
                          refreshIntervalHours: zoneValues.refreshHours,
                          scavengeServers: zoneServerList,
                        })
                      }
                    >
                      {t("common:actions.save")}
                    </Button>
                  </div>
                </div>
              ) : null}
            </div>
          </SectionCard>

          <SectionCard
            title={t("scavenging.manualTitle")}
            description={t("scavenging.manualDescription")}
          >
            <div className="flex items-center justify-between gap-4">
              <p className="text-sm text-muted-foreground">
                {canStart
                  ? t("scavenging.manualReady")
                  : t("scavenging.manualUnavailable")}
              </p>
              <Button
                variant="destructive"
                disabled={!canStart || mutation.isPending}
                onClick={() => setPendingAction({ action: "StartScavenging" })}
              >
                {t("scavenging.start")}
              </Button>
            </div>
          </SectionCard>
        </>
      ) : null}

      <ConfirmDialog
        open={Boolean(pendingAction)}
        title={t("scavenging.confirmTitle")}
        description={
          pendingAction
            ? t("scavenging.confirmations." + pendingAction.action)
            : undefined
        }
        confirmText={t("common:actions.confirm")}
        cancelText={t("common:actions.cancel")}
        variant={
          pendingAction?.action === "StartScavenging" ? "danger" : "default"
        }
        isLoading={mutation.isPending}
        onOpenChange={(open) => {
          if (!open) setPendingAction(null);
        }}
        onConfirm={() => {
          if (pendingAction) mutation.mutate(pendingAction);
        }}
      />
    </section>
  );
}

function formatDate(value: string | null | undefined, language: string) {
  if (!value) return "-";
  return new Intl.DateTimeFormat(
    language.startsWith("tr") ? "tr-TR" : "en-US",
    { dateStyle: "medium", timeStyle: "short" },
  ).format(new Date(value));
}
