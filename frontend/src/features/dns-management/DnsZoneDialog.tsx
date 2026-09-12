import { useState } from "react";
import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import type { DnsInventoryServer, DnsZoneInventory, DnsZoneKind, SaveDnsZone, UpdateDnsZone } from "./types";

type Props = {
  open: boolean;
  server: DnsInventoryServer;
  zone?: DnsZoneInventory | null;
  isLoading: boolean;
  error?: string | null;
  onOpenChange: (open: boolean) => void;
  onSubmit: (value: SaveDnsZone | UpdateDnsZone) => void;
};

const parseMasters = (value: string) => value.split(/[\s,;]+/).map((x) => x.trim()).filter(Boolean);

export function DnsZoneDialog({ open, server, zone, isLoading, error, onOpenChange, onSubmit }: Props) {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const editing = Boolean(zone);
  const initialKind = (zone?.zoneType ?? "Primary") as DnsZoneKind;
  const [name, setName] = useState(zone?.name ?? "");
  const [zoneKind, setZoneKind] = useState<DnsZoneKind>(initialKind);
  const [isDsIntegrated, setIsDsIntegrated] = useState(zone?.isDsIntegrated ?? false);
  const [dynamicUpdate, setDynamicUpdate] = useState(zone?.dynamicUpdate ?? "None");
  const [replicationScope, setReplicationScope] = useState(zone?.replicationScope ?? "Domain");
  const [directoryPartitionName, setDirectoryPartitionName] = useState(zone?.directoryPartitionName ?? "");
  const [zoneFile, setZoneFile] = useState(zone?.zoneFile ?? "");
  const [masterServers, setMasterServers] = useState((zone?.masterServers ?? []).join("\n"));
  const [forwarderTimeoutSeconds, setForwarderTimeoutSeconds] = useState(zone?.forwarderTimeoutSeconds ?? 5);
  const [useRecursion, setUseRecursion] = useState(zone?.useRecursion ?? false);
  const needsMasters = zoneKind === "Secondary" || zoneKind === "Stub" || zoneKind === "Forwarder";
  const supportsAd = zoneKind !== "Secondary";
  const effectiveAd = supportsAd && isDsIntegrated;
  const needsZoneFile = !effectiveAd && zoneKind !== "Forwarder";
  const disabled = !name.trim()
    || (needsMasters && parseMasters(masterServers).length === 0)
    || (needsZoneFile && !zoneFile.trim())
    || (effectiveAd && replicationScope === "Custom" && !directoryPartitionName.trim());

  return <Dialog open={open}>
    <DialogContent onOpenChange={onOpenChange} className="sm:max-w-2xl">
      <DialogHeader>
        <DialogTitle>{t(editing ? "zones.editTitle" : "zones.createTitle")}</DialogTitle>
        <DialogDescription>{t("zones.dialogDescription", { server: server.serverDisplayName })}</DialogDescription>
      </DialogHeader>
      <DialogBody>
        {error ? <FormError message={error} /> : null}
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2"><Label htmlFor="dns-zone-name">{t("inventory.fields.zone")}</Label><Input id="dns-zone-name" value={name} disabled={editing} placeholder="example.com" onChange={(event) => { const value = event.target.value; setName(value); if (!editing) setZoneFile(value ? `${value.replace(/\.$/, "")}.dns` : ""); }} /></div>
          <div className="space-y-2"><Label htmlFor="dns-zone-kind">{t("inventory.fields.zoneType")}</Label><Select id="dns-zone-kind" value={zoneKind} disabled={editing} onChange={(event) => { const kind = event.target.value as DnsZoneKind; setZoneKind(kind); if (kind !== "Primary") setDynamicUpdate("None"); if (kind === "Secondary") setIsDsIntegrated(false); }}>{(["Primary", "Secondary", "Stub", "Forwarder"] as const).map((kind) => <option key={kind} value={kind}>{t(`zones.types.${kind}`)}</option>)}</Select></div>
          {!editing && supportsAd ? <CheckboxField id="dns-zone-ad" label={t("zones.fields.isDsIntegrated")} checked={isDsIntegrated} onCheckedChange={(value) => { const enabled = value === true; setIsDsIntegrated(enabled); if (!enabled && dynamicUpdate === "Secure") setDynamicUpdate("None"); }} /> : null}
          {zoneKind === "Primary" ? <div className="space-y-2"><Label htmlFor="dns-zone-dynamic-update">{t("zones.fields.dynamicUpdate")}</Label><Select id="dns-zone-dynamic-update" value={dynamicUpdate} onChange={(event) => setDynamicUpdate(event.target.value)}><option value="None">{t("zones.dynamicUpdate.None")}</option><option value="NonsecureAndSecure">{t("zones.dynamicUpdate.NonsecureAndSecure")}</option>{effectiveAd ? <option value="Secure">{t("zones.dynamicUpdate.Secure")}</option> : null}</Select></div> : null}
          {!editing && effectiveAd ? <div className="space-y-2"><Label htmlFor="dns-zone-replication">{t("zones.fields.replicationScope")}</Label><Select id="dns-zone-replication" value={replicationScope} onChange={(event) => setReplicationScope(event.target.value)}><option value="Forest">{t("zones.replication.Forest")}</option><option value="Domain">{t("zones.replication.Domain")}</option><option value="Legacy">{t("zones.replication.Legacy")}</option><option value="Custom">{t("zones.replication.Custom")}</option></Select></div> : null}
          {!editing && effectiveAd && replicationScope === "Custom" ? <div className="space-y-2 sm:col-span-2"><Label htmlFor="dns-zone-partition">{t("zones.fields.directoryPartition")}</Label><Input id="dns-zone-partition" value={directoryPartitionName} onChange={(event) => setDirectoryPartitionName(event.target.value)} /></div> : null}
          {!editing && needsZoneFile ? <div className="space-y-2"><Label htmlFor="dns-zone-file">{t("zones.fields.zoneFile")}</Label><Input id="dns-zone-file" value={zoneFile} onChange={(event) => setZoneFile(event.target.value)} /></div> : null}
          {needsMasters ? <div className="space-y-2 sm:col-span-2"><Label htmlFor="dns-zone-masters">{t("zones.fields.masterServers")}</Label><Textarea id="dns-zone-masters" value={masterServers} placeholder={t("zones.masterServersPlaceholder")} onChange={(event) => setMasterServers(event.target.value)} /><p className="text-xs text-muted-foreground">{t("zones.masterServersHelp")}</p></div> : null}
          {zoneKind === "Forwarder" ? <><div className="space-y-2"><Label htmlFor="dns-zone-forwarder-timeout">{t("zones.fields.forwarderTimeout")}</Label><Input id="dns-zone-forwarder-timeout" type="number" min="0" max="15" value={forwarderTimeoutSeconds} onChange={(event) => setForwarderTimeoutSeconds(Number(event.target.value))} /></div><CheckboxField id="dns-zone-recursion" label={t("zones.fields.useRecursion")} checked={useRecursion} onCheckedChange={(value) => setUseRecursion(value === true)} /></> : null}
        </div>
        <p className="text-xs text-muted-foreground">{t("zones.optimisticConcurrencyNotice")}</p>
      </DialogBody>
      <DialogFooter>
        <Button variant="outline" disabled={isLoading} onClick={() => onOpenChange(false)}>{t("common:actions.cancel")}</Button>
        <Button disabled={isLoading || disabled} onClick={() => onSubmit(editing
          ? { dynamicUpdate: zoneKind === "Primary" ? dynamicUpdate : null, masterServers: parseMasters(masterServers), forwarderTimeoutSeconds: zoneKind === "Forwarder" ? forwarderTimeoutSeconds : null, useRecursion: zoneKind === "Forwarder" ? useRecursion : null }
          : { name: name.trim(), zoneKind, isDsIntegrated: effectiveAd, dynamicUpdate: zoneKind === "Primary" ? dynamicUpdate : null, replicationScope: effectiveAd ? replicationScope : null, directoryPartitionName: effectiveAd && replicationScope === "Custom" ? directoryPartitionName.trim() : null, zoneFile: needsZoneFile ? zoneFile.trim() : null, masterServers: parseMasters(masterServers), forwarderTimeoutSeconds: zoneKind === "Forwarder" ? forwarderTimeoutSeconds : null, useRecursion: zoneKind === "Forwarder" ? useRecursion : null })}>{isLoading ? t("zones.saving") : t("common:actions.save")}</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>;
}
