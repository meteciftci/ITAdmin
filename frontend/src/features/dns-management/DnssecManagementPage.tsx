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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { getApiErrorMessage } from "@/lib/api-error";
import {
  DNS_SERVERS_QUERY_KEY,
  DNSSEC_CONFIGURATION_QUERY_KEY,
  getDnsServers,
  getDnssecConfiguration,
  mutateDnssecConfiguration,
} from "./api";
import type { DnssecMutationInput, DnssecResolverConfiguration, DnssecZone } from "./types";

type PendingAction = Omit<DnssecMutationInput, "expectedStateToken">;

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
  const isDanger = pending?.action === "Unsign" || pending?.action === "RemoveTrustAnchorType" || (pending?.action === "SetValidationEnabled" && pending.validationEnabled === false);
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

      {configuration.data ? <ResolverPanel resolver={configuration.data.resolver} disabled={mutation.isPending} onAction={setPending} /> : null}

      <ConfirmDialog open={pending !== null} title={t("dnssec.confirmTitle", { action: actionText })} description={pending ? t(`dnssec.confirmations.${pending.action}`, { zone: pending.zoneName, trustPoint: pending.trustPointName }) : undefined} confirmText={actionText} cancelText={t("common:actions.cancel")} variant={isDanger ? "danger" : "default"} isLoading={mutation.isPending} onOpenChange={(open) => { if (!open) setPending(null); }} onConfirm={() => { if (pending) mutation.mutate(pending); }} />
    </section>
  );
}

function ResolverPanel({ resolver, disabled, onAction }: { resolver: DnssecResolverConfiguration; disabled: boolean; onAction: (value: PendingAction) => void }) {
  const { t } = useTranslation("dnsManagement");
  const [kind, setKind] = useState<"Ds" | "DnsKey">("Ds");
  const [name, setName] = useState("");
  const [algorithm, setAlgorithm] = useState("RsaSha256");
  const [keyTag, setKeyTag] = useState("");
  const [digestType, setDigestType] = useState<"Sha1" | "Sha256" | "Sha384">("Sha256");
  const [digest, setDigest] = useState("");
  const [base64Data, setBase64Data] = useState("");
  const digestLength = digestType === "Sha1" ? 40 : digestType === "Sha256" ? 64 : 96;
  const canAdd = name.trim().length > 0 && (kind === "Ds"
    ? /^\d+$/.test(keyTag) && Number(keyTag) <= 65535 && digest.length === digestLength && /^[0-9a-f]+$/i.test(digest)
    : base64Data.trim().length > 0 && base64Data.length <= 16384 && /^[A-Za-z0-9+/]+={0,2}$/.test(base64Data.trim()));
  const addAnchor = () => onAction(kind === "Ds"
    ? { action: "AddDsTrustAnchor", trustPointName: name.trim(), cryptoAlgorithm: algorithm, keyTag: Number(keyTag), digestType, digest: digest.trim() }
    : { action: "AddDnsKeyTrustAnchor", trustPointName: name.trim(), cryptoAlgorithm: algorithm, base64Data: base64Data.trim() });

  return <div className="space-y-4">
    <SectionCard title={t("dnssec.resolver.title")} description={t("dnssec.resolver.description")} actions={<Button variant="outline" disabled={disabled} onClick={() => onAction({ action: "RetrieveRootTrustAnchor" })}>{t("dnssec.actions.RetrieveRootTrustAnchor")}</Button>}>
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-4 rounded-md border p-3">
          <div><p className="font-medium">{t("dnssec.resolver.validation")}</p><p className="text-sm text-muted-foreground">{t("dnssec.resolver.validationHelp")}</p></div>
          <div className="flex items-center gap-2"><Badge variant={resolver.validationEnabled ? "success" : "secondary"}>{resolver.validationEnabled ? t("dnssec.resolver.enabled") : t("dnssec.resolver.disabled")}</Badge><Switch checked={resolver.validationEnabled} disabled={disabled} aria-label={t("dnssec.resolver.validation")} onCheckedChange={(checked) => onAction({ action: "SetValidationEnabled", validationEnabled: checked })} /></div>
        </div>
        {resolver.directoryServicesAvailable ? <div className="rounded-md border border-amber-500/40 bg-amber-500/10 p-3 text-sm">{t("dnssec.resolver.adReplicationWarning")}</div> : null}
        <dl className="grid gap-3 text-sm sm:grid-cols-3">
          <Item label={t("dnssec.resolver.storage")} value={resolver.directoryServicesAvailable ? t("dnssec.resolver.activeDirectory") : t("dnssec.resolver.localFile")} />
          <Item label={t("dnssec.resolver.readOnlyDc")} value={resolver.isReadOnlyDomainController ? t("dnssec.yes") : t("dnssec.no")} />
          <Item label={t("dnssec.resolver.rootUrl")} value={resolver.rootTrustAnchorsUrl ?? "—"} />
        </dl>
      </div>
    </SectionCard>

    <div className="grid gap-4 xl:grid-cols-[minmax(0,1.4fr)_minmax(20rem,0.8fr)]">
      <SectionCard title={t("dnssec.resolver.pointsTitle")} description={t("dnssec.resolver.pointsDescription")}>
        <div className="space-y-3">{resolver.trustPoints.map((point) => <div key={point.name} className="rounded-md border p-3">
          <div className="flex flex-wrap items-start justify-between gap-2"><div><p className="font-medium">{point.name}</p><p className="text-xs text-muted-foreground">{point.state ?? t("dnssec.unknown")} · {point.anchors.length} {t("dnssec.resolver.anchorCount")}</p></div><div className="flex gap-2">{[...new Set(point.anchors.map((x) => x.type))].filter((type): type is "DnsKey" | "Ds" => type === "DnsKey" || type === "Ds").map((type) => <Button key={type} size="sm" variant="destructive" disabled={disabled} onClick={() => onAction({ action: "RemoveTrustAnchorType", trustPointName: point.name, trustAnchorType: type })}>{t("dnssec.resolver.removeType", { type })}</Button>)}</div></div>
          <div className="mt-3 space-y-2">{point.anchors.map((anchor, index) => <div key={anchor.type + "-" + index} className="rounded bg-muted/40 p-2 text-xs"><span className="font-medium">{anchor.type}</span>{anchor.state ? " · " + anchor.state : null}<div className="mt-1 break-all font-mono text-muted-foreground">{anchor.data ?? "—"}</div></div>)}</div>
          {(point.lastActiveRefreshTime || point.nextActiveRefreshTime) ? <p className="mt-2 text-xs text-muted-foreground">{t("dnssec.resolver.refreshTimes", { last: formatDate(point.lastActiveRefreshTime), next: formatDate(point.nextActiveRefreshTime) })}</p> : null}
        </div>)}
        {resolver.trustPoints.length === 0 ? <p className="text-sm text-muted-foreground">{t("dnssec.resolver.noPoints")}</p> : null}</div>
      </SectionCard>

      <SectionCard title={t("dnssec.resolver.addTitle")} description={t("dnssec.resolver.addDescription")}>
        <div className="space-y-3">
          <div className="space-y-1"><Label htmlFor="anchor-kind">{t("dnssec.resolver.type")}</Label><Select id="anchor-kind" value={kind} onChange={(e) => setKind(e.target.value as "Ds" | "DnsKey")}><option value="Ds">DS</option><option value="DnsKey">DNSKEY</option></Select></div>
          <div className="space-y-1"><Label htmlFor="anchor-name">{t("dnssec.resolver.name")}</Label><Input id="anchor-name" value={name} maxLength={253} onChange={(e) => setName(e.target.value)} placeholder="secure.example.com" /></div>
          <div className="space-y-1"><Label htmlFor="anchor-algorithm">{t("dnssec.fields.algorithm")}</Label><Select id="anchor-algorithm" value={algorithm} onChange={(e) => setAlgorithm(e.target.value)}>{["RsaSha1","RsaSha256","RsaSha512","RsaSha1NSec3","ECDsaP256Sha256","ECDsaP384Sha384"].map((x) => <option key={x}>{x}</option>)}</Select></div>
          {kind === "Ds" ? <><div className="space-y-1"><Label htmlFor="anchor-keytag">{t("dnssec.resolver.keyTag")}</Label><Input id="anchor-keytag" type="number" min={0} max={65535} value={keyTag} onChange={(e) => setKeyTag(e.target.value)} /></div><div className="space-y-1"><Label htmlFor="anchor-digest-type">{t("dnssec.resolver.digestType")}</Label><Select id="anchor-digest-type" value={digestType} onChange={(e) => setDigestType(e.target.value as typeof digestType)}><option>Sha1</option><option>Sha256</option><option>Sha384</option></Select></div><div className="space-y-1"><Label htmlFor="anchor-digest">{t("dnssec.resolver.digest")}</Label><Textarea id="anchor-digest" value={digest} maxLength={96} onChange={(e) => setDigest(e.target.value)} /></div></> : <div className="space-y-1"><Label htmlFor="anchor-data">{t("dnssec.resolver.dnskey")}</Label><Textarea id="anchor-data" value={base64Data} maxLength={16384} onChange={(e) => setBase64Data(e.target.value)} /></div>}
          <Button disabled={disabled || !canAdd} onClick={addAnchor}>{t("dnssec.resolver.add")}</Button>
        </div>
      </SectionCard>
    </div>
  </div>;
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

function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString() : "—";
}
