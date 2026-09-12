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
import { getApiErrorMessage } from "@/lib/api-error";
import {
  DNS_SERVERS_QUERY_KEY,
  DNSSEC_CONFIGURATION_QUERY_KEY,
  getDnsServers,
  getDnssecConfiguration,
  mutateDnssecConfiguration,
} from "./api";
import type { DnssecMutationInput, DnssecZone } from "./types";

type PendingAction = Pick<DnssecMutationInput, "action" | "zoneName" | "keyIds">;

export function DnssecManagementPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const queryClient = useQueryClient();
  const [serverId, setServerId] = useState("");
  const [selectedZoneName, setSelectedZoneName] = useState("");
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [error, setError] = useState<string | null>(null);
  const servers = useQuery({ queryKey: DNS_SERVERS_QUERY_KEY, queryFn: getDnsServers });
  const activeServers = useMemo(() => servers.data?.filter((x) => x.isEnabled) ?? [], [servers.data]);
  const selectedServerId = serverId || activeServers[0]?.id || "";
  const configuration = useQuery({
    queryKey: [...DNSSEC_CONFIGURATION_QUERY_KEY, selectedServerId],
    queryFn: () => getDnssecConfiguration(selectedServerId),
    enabled: Boolean(selectedServerId),
  });
  const zones = configuration.data?.zones ?? [];
  const selectedZone = zones.find((x) => x.name === selectedZoneName) ?? zones.find((x) => x.isEligibleForSigning) ?? zones[0];

  const mutation = useMutation({
    mutationFn: (request: PendingAction) => mutateDnssecConfiguration(selectedServerId, {
      ...request,
      keyIds: request.keyIds ?? [],
      expectedStateToken: configuration.data!.stateToken,
    }),
    onSuccess: async (result) => {
      setPending(null);
      setError(null);
      if (result.configuration) queryClient.setQueryData([...DNSSEC_CONFIGURATION_QUERY_KEY, selectedServerId], result.configuration);
      else await configuration.refetch();
      toast.success(result.message || t("dnssec.updated"));
    },
    onError: (failure) => {
      setPending(null);
      setError(getApiErrorMessage(failure, t("dnssec.operationFailed")));
      configuration.refetch();
    },
  });

  const actionText = pending ? t(`dnssec.actions.${pending.action}`) : "";
  const isDanger = pending?.action === "Unsign";
  return (
    <section className="space-y-4">
      <PageHeader title={t("dnssec.title")} description={t("dnssec.description")} actions={selectedServerId ? <Button variant="outline" disabled={configuration.isFetching || mutation.isPending} onClick={() => configuration.refetch()}>{t("common:actions.refresh")}</Button> : null} />
      <FormError message={error ?? (servers.isError || configuration.isError ? t("dnssec.loadFailed") : null)} />

      <SectionCard title={t("dnssec.serverTitle")} description={t("dnssec.liveNotice")}>
        {servers.isLoading ? <LoadingState /> : <div className="max-w-xl space-y-2"><Label htmlFor="dnssec-server">{t("dnssec.server")}</Label><Select id="dnssec-server" value={selectedServerId} disabled={mutation.isPending} onChange={(event) => { setServerId(event.target.value); setSelectedZoneName(""); setError(null); }}><option value="">{t("dnssec.selectServer")}</option>{activeServers.map((server) => <option key={server.id} value={server.id}>{server.displayName} ({server.hostName})</option>)}</Select></div>}
      </SectionCard>

      {configuration.isLoading ? <LoadingState /> : null}
      {configuration.data ? <div className="grid gap-4 xl:grid-cols-[minmax(18rem,0.8fr)_minmax(0,1.7fr)]">
        <SectionCard title={t("dnssec.zonesTitle")} description={t("dnssec.zonesDescription")}>
          <div className="max-h-[38rem] space-y-2 overflow-y-auto">
            {zones.map((zone) => <button key={zone.name} type="button" className={`w-full rounded-md border p-3 text-left transition-colors ${selectedZone?.name === zone.name ? "border-primary bg-primary/5" : "hover:bg-muted/50"}`} onClick={() => setSelectedZoneName(zone.name)}>
              <div className="flex items-center justify-between gap-2"><span className="truncate font-medium">{zone.name}</span><Badge variant={zone.isSigned ? "success" : "secondary"}>{zone.isSigned ? t("dnssec.signed") : t("dnssec.unsigned")}</Badge></div>
              <p className="mt-1 text-xs text-muted-foreground">{zone.zoneType} · {zone.isDsIntegrated ? t("dnssec.adIntegrated") : t("dnssec.fileBacked")}</p>
            </button>)}
            {zones.length === 0 ? <p className="text-sm text-muted-foreground">{t("dnssec.noZones")}</p> : null}
          </div>
        </SectionCard>

        {selectedZone ? <ZoneDetails zone={selectedZone} disabled={mutation.isPending} onAction={setPending} /> : null}
      </div> : null}

      <ConfirmDialog open={pending !== null} title={t("dnssec.confirmTitle", { action: actionText })} description={pending ? t(`dnssec.confirmations.${pending.action}`, { zone: pending.zoneName }) : undefined} confirmText={actionText} cancelText={t("common:actions.cancel")} variant={isDanger ? "danger" : "default"} isLoading={mutation.isPending} onOpenChange={(open) => { if (!open) setPending(null); }} onConfirm={() => { if (pending) mutation.mutate(pending); }} />
    </section>
  );
}

function ZoneDetails({ zone, disabled, onAction }: { zone: DnssecZone; disabled: boolean; onAction: (value: PendingAction) => void }) {
  const { t } = useTranslation("dnsManagement");
  return <div className="space-y-4">
    <SectionCard title={zone.name} description={zone.isEligibleForSigning ? t("dnssec.eligible") : t(`dnssec.reasons.${zone.ineligibilityReason ?? "Unknown"}`)} actions={<div className="flex flex-wrap gap-2">{!zone.isSigned ? <Button disabled={disabled || !zone.isEligibleForSigning} onClick={() => onAction({ action: "SignWithDefaults", zoneName: zone.name })}>{t("dnssec.actions.SignWithDefaults")}</Button> : <><Button variant="outline" disabled={disabled || !zone.isEligibleForSigning} onClick={() => onAction({ action: "Resign", zoneName: zone.name })}>{t("dnssec.actions.Resign")}</Button><Button variant="destructive" disabled={disabled || !zone.isEligibleForSigning} onClick={() => onAction({ action: "Unsign", zoneName: zone.name })}>{t("dnssec.actions.Unsign")}</Button></>}</div>}>
      {!zone.isSigned ? <div className="rounded-md border border-amber-500/40 bg-amber-500/10 p-3 text-sm leading-6">{t("dnssec.parentDsWarning")}</div> : <div className="space-y-4">{zone.parentHasSecureDelegation === false ? <div className="rounded-md border border-amber-500/40 bg-amber-500/10 p-3 text-sm leading-6">{t("dnssec.missingParentDsWarning")}</div> : null}<dl className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
        <Item label={t("dnssec.fields.keyMaster")} value={zone.keyMasterServer ?? "—"} />
        <Item label={t("dnssec.fields.keyMasterStatus")} value={zone.keyMasterStatus ?? "—"} />
        <Item label={t("dnssec.fields.denial")} value={zone.denialOfExistence ?? "—"} />
        <Item label={t("dnssec.fields.parentDelegation")} value={zone.parentHasSecureDelegation == null ? t("dnssec.unknown") : zone.parentHasSecureDelegation ? t("dnssec.yes") : t("dnssec.no")} />
        <Item label={t("dnssec.fields.dnskeyTtl")} value={formatSeconds(zone.dnsKeyRecordSetTtlSeconds, t("dnssec.seconds"))} />
        <Item label={t("dnssec.fields.dsTtl")} value={formatSeconds(zone.dsRecordSetTtlSeconds, t("dnssec.seconds"))} />
        <Item label={t("dnssec.fields.dsAlgorithms")} value={zone.dsRecordGenerationAlgorithms.join(", ") || "—"} />
      </dl></div>}
    </SectionCard>

    {zone.isSigned ? <SectionCard title={t("dnssec.keysTitle")} description={t("dnssec.keysDescription")}>
      <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left"><th className="p-2">{t("dnssec.fields.keyType")}</th><th className="p-2">{t("dnssec.fields.algorithm")}</th><th className="p-2">{t("dnssec.fields.keyLength")}</th><th className="p-2">{t("dnssec.fields.status")}</th><th className="p-2">{t("dnssec.fields.nextRollover")}</th><th className="p-2 text-right">{t("dnssec.fields.operation")}</th></tr></thead><tbody>{zone.signingKeys.map((key) => <tr key={key.keyId} className="border-b last:border-0"><td className="p-2"><div className="font-medium">{key.keyType}</div><div className="font-mono text-xs text-muted-foreground">{key.keyId}</div></td><td className="p-2">{key.cryptoAlgorithm ?? "—"}</td><td className="p-2">{key.keyLength ?? "—"}</td><td className="p-2">{key.keyStatus ?? "—"}</td><td className="p-2">{key.nextRolloverTime ? new Date(key.nextRolloverTime).toLocaleString() : "—"}</td><td className="p-2 text-right"><Button size="sm" variant="outline" disabled={disabled} onClick={() => onAction({ action: "RolloverKeys", zoneName: zone.name, keyIds: [key.keyId] })}>{t("dnssec.actions.RolloverKeys")}</Button></td></tr>)}</tbody></table></div>
      {zone.signingKeys.length === 0 ? <p className="text-sm text-muted-foreground">{t("dnssec.noKeys")}</p> : null}
    </SectionCard> : null}
  </div>;
}

function Item({ label, value }: { label: string; value: string }) {
  return <div><dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt><dd className="mt-1 break-words font-medium">{value}</dd></div>;
}

function formatSeconds(value: number | null | undefined, suffix: string) {
  if (value == null) return "—";
  return `${value} ${suffix}`;
}
