import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { FormError } from "@/components/common/FormError";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { getApiErrorMessage } from "@/lib/api-error";
import {
  DNS_SERVERS_QUERY_KEY, DNS_ZONE_TRANSFER_CONFIGURATION_QUERY_KEY,
  getDnsServers, getDnsZoneTransferConfiguration, updateDnsZoneTransferConfiguration,
} from "./api";
import type { DnsZoneNotifyMode, DnsZoneTransferMode, DnsZoneTransferMutationInput, DnsZoneTransferSetting } from "./types";

type Draft = Omit<DnsZoneTransferMutationInput, "expectedStateToken"> & { secondaryText: string; notifyText: string };
const splitAddresses = (value: string) => value.split(/[\r\n,;]+/).map((x) => x.trim()).filter(Boolean);

export function DnsZoneTransfersPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const queryClient = useQueryClient();
  const [serverId, setServerId] = useState("");
  const [draft, setDraft] = useState<Draft | null>(null);
  const [pending, setPending] = useState<DnsZoneTransferMutationInput | null>(null);
  const [error, setError] = useState<string | null>(null);
  const servers = useQuery({ queryKey: DNS_SERVERS_QUERY_KEY, queryFn: getDnsServers });
  const activeServers = useMemo(() => servers.data?.filter((x) => x.isEnabled) ?? [], [servers.data]);
  const selectedServerId = serverId || activeServers[0]?.id || "";
  const configuration = useQuery({
    queryKey: [...DNS_ZONE_TRANSFER_CONFIGURATION_QUERY_KEY, selectedServerId],
    queryFn: () => getDnsZoneTransferConfiguration(selectedServerId), enabled: Boolean(selectedServerId),
  });

  const mutation = useMutation({
    mutationFn: (request: DnsZoneTransferMutationInput) => updateDnsZoneTransferConfiguration(selectedServerId, request),
    onSuccess: (result) => {
      if (result.configuration) queryClient.setQueryData([...DNS_ZONE_TRANSFER_CONFIGURATION_QUERY_KEY, selectedServerId], result.configuration);
      setDraft(null); setPending(null); setError(null); toast.success(t("zoneTransfers.updated"));
    },
    onError: (failure) => { setPending(null); setError(getApiErrorMessage(failure, t("zoneTransfers.operationFailed"))); },
  });

  const edit = (zone: DnsZoneTransferSetting) => setDraft({
    zoneName: zone.zoneName, transferMode: zone.transferMode, secondaryServers: zone.secondaryServers,
    notifyMode: zone.notifyMode, notifyServers: zone.notifyServers,
    secondaryText: zone.secondaryServers.join("\n"), notifyText: zone.notifyServers.join("\n"),
  });
  const prepare = () => {
    if (!draft || !configuration.data) return;
    const secondaryServers = splitAddresses(draft.secondaryText);
    const notifyServers = splitAddresses(draft.notifyText);
    setPending({
      zoneName: draft.zoneName, transferMode: draft.transferMode,
      secondaryServers: draft.transferMode === "TransferToSecureServers" ? secondaryServers : [],
      notifyMode: draft.notifyMode, notifyServers: draft.notifyMode === "NotifyServers" ? notifyServers : [],
      expectedStateToken: configuration.data.stateToken,
    });
  };
  const valid = Boolean(draft)
    && (draft!.transferMode !== "TransferToSecureServers" || splitAddresses(draft!.secondaryText).length > 0)
    && (draft!.notifyMode !== "NotifyServers" || splitAddresses(draft!.notifyText).length > 0);

  return <section className="space-y-4">
    <PageHeader title={t("zoneTransfers.title")} description={t("zoneTransfers.description")}
      actions={selectedServerId ? <Button variant="outline" disabled={configuration.isFetching || mutation.isPending} onClick={() => { setDraft(null); configuration.refetch(); }}>{t("common:actions.refresh")}</Button> : null} />
    <FormError message={error ?? (servers.isError || configuration.isError ? t("zoneTransfers.loadFailed") : null)} />
    <SectionCard title={t("zoneTransfers.serverTitle")} description={t("zoneTransfers.liveNotice")}>
      {servers.isLoading ? <LoadingState /> : <div className="max-w-xl space-y-2"><Label htmlFor="dns-transfer-server">{t("zoneTransfers.fields.server")}</Label>
        <Select id="dns-transfer-server" value={selectedServerId} disabled={mutation.isPending} onChange={(e) => { setServerId(e.target.value); setDraft(null); setError(null); }}>
          <option value="">{t("zoneTransfers.selectServer")}</option>{activeServers.map((x) => <option key={x.id} value={x.id}>{x.displayName} ({x.hostName})</option>)}
        </Select></div>}
    </SectionCard>
    {configuration.isLoading ? <LoadingState /> : null}
    {configuration.data ? <SectionCard title={t("zoneTransfers.zonesTitle")} description={t("zoneTransfers.zonesDescription")}>
      <div className="mb-4 rounded-lg border border-amber-500/40 bg-amber-500/10 p-4 text-sm"><p className="font-medium">{t("zoneTransfers.warningTitle")}</p><p className="mt-1 text-muted-foreground">{t("zoneTransfers.warningDescription")}</p></div>
      <div className="space-y-2">{configuration.data.zones.map((zone) => <div key={zone.zoneName} className="flex flex-col gap-3 rounded-md border p-3 sm:flex-row sm:items-center sm:justify-between">
        <div><p className="font-mono text-sm font-medium">{zone.zoneName}</p><div className="mt-1 flex flex-wrap gap-1"><Badge variant={zone.transferMode === "TransferAnyServer" ? "destructive" : "outline"}>{t(`zoneTransfers.transferModes.${zone.transferMode}`)}</Badge><Badge variant="secondary">{t(`zoneTransfers.notifyModes.${zone.notifyMode}`)}</Badge>{zone.isDsIntegrated ? <Badge variant="outline">AD</Badge> : null}</div></div>
        <Button size="sm" variant="outline" disabled={mutation.isPending} onClick={() => edit(zone)}>{t("common:actions.edit")}</Button>
      </div>)}{configuration.data.zones.length === 0 ? <p className="text-sm text-muted-foreground">{t("zoneTransfers.noZones")}</p> : null}</div>
      {draft ? <div className="mt-4 space-y-4 rounded-lg border p-4">
        <p className="font-medium">{draft.zoneName}</p>
        <div className="grid gap-4 md:grid-cols-2"><div className="space-y-2"><Label htmlFor="dns-transfer-mode">{t("zoneTransfers.fields.transferMode")}</Label><Select id="dns-transfer-mode" value={draft.transferMode} disabled={mutation.isPending} onChange={(e) => setDraft({ ...draft, transferMode: e.target.value as DnsZoneTransferMode })}>{(["NoTransfer", "TransferToZoneNameServer", "TransferToSecureServers", "TransferAnyServer"] as DnsZoneTransferMode[]).map((x) => <option key={x} value={x}>{t(`zoneTransfers.transferModes.${x}`)}</option>)}</Select></div>
          <div className="space-y-2"><Label htmlFor="dns-notify-mode">{t("zoneTransfers.fields.notifyMode")}</Label><Select id="dns-notify-mode" value={draft.notifyMode} disabled={mutation.isPending} onChange={(e) => setDraft({ ...draft, notifyMode: e.target.value as DnsZoneNotifyMode })}>{(["NoNotify", "Notify", "NotifyServers"] as DnsZoneNotifyMode[]).map((x) => <option key={x} value={x}>{t(`zoneTransfers.notifyModes.${x}`)}</option>)}</Select></div></div>
        {draft.transferMode === "TransferAnyServer" ? <div className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">{t("zoneTransfers.anyServerRisk")}</div> : null}
        {draft.transferMode === "TransferToSecureServers" ? <div className="space-y-2"><Label htmlFor="dns-secondary-servers">{t("zoneTransfers.fields.secondaryServers")}</Label><Textarea id="dns-secondary-servers" rows={4} value={draft.secondaryText} onChange={(e) => setDraft({ ...draft, secondaryText: e.target.value })} /><p className="text-xs text-muted-foreground">{t("zoneTransfers.addressHelp")}</p></div> : null}
        {draft.notifyMode === "NotifyServers" ? <div className="space-y-2"><Label htmlFor="dns-notify-servers">{t("zoneTransfers.fields.notifyServers")}</Label><Textarea id="dns-notify-servers" rows={4} value={draft.notifyText} onChange={(e) => setDraft({ ...draft, notifyText: e.target.value })} /><p className="text-xs text-muted-foreground">{t("zoneTransfers.addressHelp")}</p></div> : null}
        <div className="flex justify-end gap-2"><Button variant="outline" disabled={mutation.isPending} onClick={() => setDraft(null)}>{t("common:actions.cancel")}</Button><Button disabled={!valid || mutation.isPending} onClick={prepare}>{t("common:actions.save")}</Button></div>
      </div> : null}
    </SectionCard> : null}
    <ConfirmDialog open={Boolean(pending)} title={t("zoneTransfers.confirmTitle")} description={pending ? t(pending.transferMode === "TransferAnyServer" ? "zoneTransfers.confirmAnyServer" : "zoneTransfers.confirmDescription", { zone: pending.zoneName }) : undefined} confirmText={t("common:actions.confirm")} cancelText={t("common:actions.cancel")} variant="danger" isLoading={mutation.isPending} onOpenChange={(open) => { if (!open) setPending(null); }} onConfirm={() => { if (pending) mutation.mutate(pending); }} />
  </section>;
}
