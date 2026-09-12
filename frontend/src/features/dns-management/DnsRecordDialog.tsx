import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import type { CreateDnsRecord, DnsRecordInventory, DnsRecordMutationInput, DnsZoneInventory } from "./types";

const writableDnsRecordTypes = ["A", "AAAA", "CNAME", "MX", "NS", "PTR", "SRV", "TXT"] as const;

type Props = {
  open: boolean;
  zone: DnsZoneInventory;
  record?: DnsRecordInventory | null;
  isLoading: boolean;
  error?: string | null;
  onOpenChange: (open: boolean) => void;
  onSubmit: (value: CreateDnsRecord | DnsRecordMutationInput) => void;
};

const emptyValues = (type: string) => Array.from({ length: type === "MX" ? 2 : type === "SRV" ? 4 : 1 }, () => "");

function readValues(record: DnsRecordInventory): string[] {
  try {
    const data = JSON.parse(record.recordDataJson) as Record<string, unknown>;
    const text = (key: string) => data[key] == null ? "" : String(data[key]);
    switch (record.recordType) {
      case "A": return [text("IPv4Address")];
      case "AAAA": return [text("IPv6Address")];
      case "CNAME": return [text("HostNameAlias")];
      case "MX": return [text("Preference"), text("MailExchange")];
      case "NS": return [text("NameServer")];
      case "PTR": return [text("PtrDomainName")];
      case "SRV": return [text("Priority"), text("Weight"), text("Port"), text("DomainName")];
      case "TXT": return [Array.isArray(data.DescriptiveText) ? data.DescriptiveText.map(String).join("") : text("DescriptiveText")];
      default: return [""];
    }
  } catch {
    return [""];
  }
}

const fieldKeys: Record<string, string[]> = {
  A: ["ipv4"], AAAA: ["ipv6"], CNAME: ["target"], MX: ["preference", "mailExchange"],
  NS: ["nameServer"], PTR: ["ptrTarget"], SRV: ["priority", "weight", "port", "target"], TXT: ["text"],
};

export function DnsRecordDialog({ open, zone, record, isLoading, error, onOpenChange, onSubmit }: Props) {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const editing = Boolean(record);
  const [relativeName, setRelativeName] = useState(record?.relativeName ?? "@");
  const [recordType, setRecordType] = useState<string>(record?.recordType ?? "A");
  const [values, setValues] = useState<string[]>(record ? readValues(record) : [""]);
  const [ttl, setTtl] = useState(record?.timeToLiveSeconds ?? 3600);
  const [zoneScope, setZoneScope] = useState(record?.zoneScope ?? "");

  const labels = useMemo(() => fieldKeys[recordType] ?? ["value"], [recordType]);
  const disabled = !relativeName.trim() || ttl < 0 || values.some((value) => !value.trim());

  return <Dialog open={open}>
    <DialogContent onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t(editing ? "records.editTitle" : "records.createTitle")}</DialogTitle>
        <DialogDescription>{t("records.dialogDescription", { zone: zone.name, server: zone.serverDisplayName })}</DialogDescription>
      </DialogHeader>
      <DialogBody>
        {error ? <FormError message={error} /> : null}
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2"><Label htmlFor="dns-record-name">{t("inventory.fields.owner")}</Label><Input id="dns-record-name" value={relativeName} disabled={editing} onChange={(event) => setRelativeName(event.target.value)} /></div>
          <div className="space-y-2"><Label htmlFor="dns-record-type">{t("inventory.fields.recordType")}</Label><Select id="dns-record-type" value={recordType} disabled={editing} onChange={(event) => { setRecordType(event.target.value); setValues(emptyValues(event.target.value)); }}>{writableDnsRecordTypes.map((type) => <option key={type} value={type}>{type}</option>)}</Select></div>
          <div className="space-y-2"><Label htmlFor="dns-record-ttl">{t("inventory.fields.ttl")}</Label><Input id="dns-record-ttl" type="number" min="0" max="2147483" value={ttl} onChange={(event) => setTtl(Number(event.target.value))} /></div>
          {!editing && zone.zoneScopes.length ? <div className="space-y-2"><Label htmlFor="dns-record-scope">{t("inventory.fields.scope")}</Label><Select id="dns-record-scope" value={zoneScope} onChange={(event) => setZoneScope(event.target.value)}><option value="">{t("inventory.defaultScope")}</option>{zone.zoneScopes.map((scope) => <option key={scope} value={scope}>{scope}</option>)}</Select></div> : null}
        </div>
        <div className="grid gap-4 sm:grid-cols-2">{labels.map((key, index) => {
          const numeric = ["preference", "priority", "weight", "port"].includes(key);
          return <div className="space-y-2" key={`${recordType}-${key}`}><Label htmlFor={`dns-record-value-${index}`}>{t(`records.fields.${key}`)}</Label><Input id={`dns-record-value-${index}`} type={numeric ? "number" : "text"} min={numeric ? 0 : undefined} max={numeric ? 65535 : undefined} value={values[index] ?? ""} onChange={(event) => setValues((current) => current.map((value, valueIndex) => valueIndex === index ? event.target.value : value))} /></div>;
        })}</div>
        <p className="text-xs text-muted-foreground">{t("records.optimisticConcurrencyNotice")}</p>
      </DialogBody>
      <DialogFooter>
        <Button variant="outline" disabled={isLoading} onClick={() => onOpenChange(false)}>{t("common:actions.cancel")}</Button>
        <Button disabled={isLoading || disabled} onClick={() => onSubmit(editing
          ? { values: values.map((value) => value.trim()), timeToLiveSeconds: ttl }
          : { relativeName: relativeName.trim(), recordType, values: values.map((value) => value.trim()), timeToLiveSeconds: ttl, zoneScope: zoneScope || null })}>{isLoading ? t("records.saving") : t("common:actions.save")}</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>;
}
