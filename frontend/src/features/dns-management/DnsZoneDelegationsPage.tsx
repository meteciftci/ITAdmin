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
import { Textarea } from "@/components/ui/textarea";
import { getApiErrorMessage } from "@/lib/api-error";
import {
  DNS_SERVERS_QUERY_KEY, DNS_ZONE_DELEGATION_CONFIGURATION_QUERY_KEY,
  getDnsServers, getDnsZoneDelegationConfiguration, mutateDnsZoneDelegationConfiguration,
} from "./api";
import type { DnsZoneDelegationMutationInput } from "./types";

const splitAddresses = (value: string) => value.split(/[\r\n,;]+/).map((x) => x.trim()).filter(Boolean);

export function DnsZoneDelegationsPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const queryClient = useQueryClient();
  const [serverId, setServerId] = useState("");
  const [parentZone, setParentZone] = useState("");
  const [childZone, setChildZone] = useState("");
  const [nameServer, setNameServer] = useState("");
  const [ipText, setIpText] = useState("");
  const [editing, setEditing] = useState<{ childZoneName: string; nameServer: string; ipText: string } | null>(null);
  const [pending, setPending] = useState<DnsZoneDelegationMutationInput | null>(null);
  const [error, setError] = useState<string | null>(null);
  const servers = useQuery({ queryKey: DNS_SERVERS_QUERY_KEY, queryFn: getDnsServers });
  const activeServers = useMemo(() => servers.data?.filter((x) => x.isEnabled) ?? [], [servers.data]);
  const selectedServerId = serverId || activeServers[0]?.id || "";
  const configuration = useQuery({
    queryKey: [...DNS_ZONE_DELEGATION_CONFIGURATION_QUERY_KEY, selectedServerId],
    queryFn: () => getDnsZoneDelegationConfiguration(selectedServerId), enabled: Boolean(selectedServerId),
  });
  const selectedParent = parentZone || configuration.data?.parentZones[0] || "";
  const visibleDelegations = configuration.data?.delegations.filter((x) => x.parentZoneName === selectedParent) ?? [];

  const mutation = useMutation({
    mutationFn: (request: DnsZoneDelegationMutationInput) => mutateDnsZoneDelegationConfiguration(selectedServerId, request),
    onSuccess: (result) => {
      if (result.configuration) queryClient.setQueryData([...DNS_ZONE_DELEGATION_CONFIGURATION_QUERY_KEY, selectedServerId], result.configuration);
      setPending(null); setEditing(null); setError(null); setChildZone(""); setNameServer(""); setIpText("");
      toast.success(t("zoneDelegations.updated"));
    },
    onError: (failure) => { setPending(null); setError(getApiErrorMessage(failure, t("zoneDelegations.operationFailed"))); },
  });

  const prepareAdd = () => {
    if (!configuration.data || !selectedParent) return;
    setPending({ action: "AddNameServer", parentZoneName: selectedParent, childZoneName: childZone.trim(),
      nameServer: nameServer.trim(), ipAddresses: splitAddresses(ipText), expectedStateToken: configuration.data.stateToken });
  };
  const prepareExisting = (action: "RemoveNameServer" | "DeleteDelegation", child: string, server?: string) => {
    if (!configuration.data) return;
    setPending({ action, parentZoneName: selectedParent, childZoneName: child,
      nameServer: server, ipAddresses: [], expectedStateToken: configuration.data.stateToken });
  };
  const prepareUpdate = () => {
    if (!configuration.data || !editing) return;
    setPending({ action: "UpdateNameServerAddresses", parentZoneName: selectedParent,
      childZoneName: editing.childZoneName, nameServer: editing.nameServer,
      ipAddresses: splitAddresses(editing.ipText), expectedStateToken: configuration.data.stateToken });
  };
  const addValid = Boolean(selectedParent && childZone.trim() && nameServer.trim() && splitAddresses(ipText).length);

  return <section className="space-y-4">
    <PageHeader title={t("zoneDelegations.title")} description={t("zoneDelegations.description")}
      actions={selectedServerId ? <Button variant="outline" disabled={configuration.isFetching || mutation.isPending} onClick={() => { setError(null); configuration.refetch(); }}>{t("common:actions.refresh")}</Button> : null} />
    <FormError message={error ?? (servers.isError || configuration.isError ? t("zoneDelegations.loadFailed") : null)} />
    <SectionCard title={t("zoneDelegations.serverTitle")} description={t("zoneDelegations.liveNotice")}>
      {servers.isLoading ? <LoadingState /> : <div className="grid max-w-4xl gap-4 md:grid-cols-2">
        <div className="space-y-2"><Label htmlFor="dns-delegation-server">{t("zoneDelegations.fields.server")}</Label>
          <Select id="dns-delegation-server" value={selectedServerId} disabled={mutation.isPending} onChange={(e) => { setServerId(e.target.value); setParentZone(""); setError(null); }}>
            <option value="">{t("zoneDelegations.selectServer")}</option>{activeServers.map((x) => <option key={x.id} value={x.id}>{x.displayName} ({x.hostName})</option>)}
          </Select></div>
        {configuration.data ? <div className="space-y-2"><Label htmlFor="dns-parent-zone">{t("zoneDelegations.fields.parentZone")}</Label>
          <Select id="dns-parent-zone" value={selectedParent} disabled={mutation.isPending} onChange={(e) => setParentZone(e.target.value)}>
            {configuration.data.parentZones.map((x) => <option key={x} value={x}>{x}</option>)}
          </Select></div> : null}
      </div>}
    </SectionCard>
    {configuration.isLoading ? <LoadingState /> : null}
    {configuration.data && selectedParent ? <>
      <SectionCard title={t("zoneDelegations.addTitle")} description={t("zoneDelegations.addDescription")}>
        <div className="grid gap-4 lg:grid-cols-3">
          <div className="space-y-2"><Label htmlFor="dns-child-zone">{t("zoneDelegations.fields.childZone")}</Label><Input id="dns-child-zone" value={childZone} disabled={mutation.isPending} onChange={(e) => setChildZone(e.target.value)} placeholder="subdomain" /><p className="text-xs text-muted-foreground">{t("zoneDelegations.childHelp", { parent: selectedParent })}</p></div>
          <div className="space-y-2"><Label htmlFor="dns-delegation-ns">{t("zoneDelegations.fields.nameServer")}</Label><Input id="dns-delegation-ns" value={nameServer} disabled={mutation.isPending} onChange={(e) => setNameServer(e.target.value)} placeholder={`ns1.${selectedParent}`} /></div>
          <div className="space-y-2"><Label htmlFor="dns-delegation-ips">{t("zoneDelegations.fields.ipAddresses")}</Label><Textarea id="dns-delegation-ips" rows={3} value={ipText} disabled={mutation.isPending} onChange={(e) => setIpText(e.target.value)} placeholder="192.0.2.10" /><p className="text-xs text-muted-foreground">{t("zoneDelegations.addressHelp")}</p></div>
        </div>
        <div className="mt-4 flex justify-end"><Button disabled={!addValid || mutation.isPending} onClick={prepareAdd}>{t("zoneDelegations.addNameServer")}</Button></div>
      </SectionCard>
      <SectionCard title={t("zoneDelegations.listTitle")} description={t("zoneDelegations.listDescription", { parent: selectedParent })}>
        <div className="space-y-3">{visibleDelegations.map((delegation) => <div key={`${delegation.parentZoneName}/${delegation.childZoneName}`} className="rounded-lg border p-4">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between"><div><p className="font-mono font-medium">{delegation.childZoneName}.{delegation.parentZoneName}</p><p className="mt-1 text-sm text-muted-foreground">{t("zoneDelegations.nameServerCount", { count: delegation.nameServers.length })}</p></div>
            <Button size="sm" variant="destructive" disabled={mutation.isPending} onClick={() => prepareExisting("DeleteDelegation", delegation.childZoneName)}>{t("zoneDelegations.deleteDelegation")}</Button></div>
          <div className="mt-3 space-y-2">{delegation.nameServers.map((ns) => <div key={ns.nameServer} className="rounded-md bg-muted/40 p-3">
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <div><p className="font-mono text-sm">{ns.nameServer}</p><div className="mt-1 flex flex-wrap gap-1">{ns.ipAddresses.map((ip) => <Badge key={ip} variant="outline">{ip}</Badge>)}{ns.ipAddresses.length === 0 ? <span className="text-xs text-muted-foreground">{t("zoneDelegations.noGlue")}</span> : null}</div></div>
              <div className="flex gap-2"><Button size="sm" variant="outline" disabled={mutation.isPending} onClick={() => setEditing({ childZoneName: delegation.childZoneName, nameServer: ns.nameServer, ipText: ns.ipAddresses.join("\n") })}>{t("zoneDelegations.editAddresses")}</Button>
                <Button size="sm" variant="outline" disabled={mutation.isPending || delegation.nameServers.length <= 1} title={delegation.nameServers.length <= 1 ? t("zoneDelegations.lastServerHelp") : undefined} onClick={() => prepareExisting("RemoveNameServer", delegation.childZoneName, ns.nameServer)}>{t("zoneDelegations.removeNameServer")}</Button></div>
            </div>
            {editing?.childZoneName === delegation.childZoneName && editing.nameServer === ns.nameServer ? <div className="mt-3 space-y-2 border-t pt-3"><Label htmlFor="dns-edit-glue">{t("zoneDelegations.fields.ipAddresses")}</Label><Textarea id="dns-edit-glue" rows={3} value={editing.ipText} disabled={mutation.isPending} onChange={(e) => setEditing({ ...editing, ipText: e.target.value })} /><div className="flex justify-end gap-2"><Button size="sm" variant="outline" onClick={() => setEditing(null)}>{t("common:actions.cancel")}</Button><Button size="sm" disabled={!splitAddresses(editing.ipText).length || mutation.isPending} onClick={prepareUpdate}>{t("common:actions.save")}</Button></div></div> : null}
          </div>)}</div>
        </div>)}{visibleDelegations.length === 0 ? <p className="text-sm text-muted-foreground">{t("zoneDelegations.noDelegations")}</p> : null}</div>
      </SectionCard>
    </> : null}
    <ConfirmDialog open={Boolean(pending)} title={pending ? t(`zoneDelegations.confirm.${pending.action}.title`) : ""}
      description={pending ? t(`zoneDelegations.confirm.${pending.action}.description`, { child: `${pending.childZoneName}.${pending.parentZoneName}`, server: pending.nameServer }) : undefined}
      confirmText={t("common:actions.confirm")} cancelText={t("common:actions.cancel")} variant="danger" isLoading={mutation.isPending}
      onOpenChange={(open) => { if (!open) setPending(null); }} onConfirm={() => { if (pending) mutation.mutate(pending); }} />
  </section>;
}
