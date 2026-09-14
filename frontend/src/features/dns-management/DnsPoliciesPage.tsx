import { useId, useMemo, useState } from "react";
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
import { getApiErrorMessage } from "@/lib/api-error";
import { DNS_POLICY_CONFIGURATION_QUERY_KEY, DNS_SERVERS_QUERY_KEY, getDnsPolicyConfiguration, getDnsServers, mutateDnsPolicyConfiguration } from "./api";
import type { DnsPolicyMutationInput } from "./types";

const split = (value: string) => value.split(/[\n,;]+/).map((x) => x.trim()).filter(Boolean);
type PendingDelete = Pick<DnsPolicyMutationInput, "action" | "name" | "zoneName" | "level">;

export function DnsPoliciesPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const queryClient = useQueryClient();
  const [serverId, setServerId] = useState("");
  const [subnet, setSubnet] = useState({ name: "", ipv4: "", ipv6: "" });
  const [scope, setScope] = useState({ zoneName: "", name: "" });
  const [policy, setPolicy] = useState({ name: "", level: "Zone" as "Server" | "Zone", zoneName: "", decision: "Allow" as "Allow" | "Deny" | "Ignore", condition: "And" as "And" | "Or", matchOperator: "Eq" as "Eq" | "Ne", order: 1, clientSubnets: "", fqdns: "", queryTypes: "", transportProtocols: "", internetProtocols: "", serverInterfaces: "", zoneScopes: "" });
  const [transferPolicy, setTransferPolicy] = useState({ name: "", level: "Zone" as "Server" | "Zone", zoneName: "", decision: "Deny" as "Deny" | "Ignore", condition: "And" as "And" | "Or", matchOperator: "Eq" as "Eq" | "Ne", order: 1, clientSubnets: "", transportProtocols: "", internetProtocols: "", serverInterfaces: "", timeOfDay: "" });
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null);
  const [error, setError] = useState<string | null>(null);
  const servers = useQuery({ queryKey: DNS_SERVERS_QUERY_KEY, queryFn: getDnsServers });
  const activeServers = useMemo(() => servers.data?.filter((x) => x.isEnabled) ?? [], [servers.data]);
  const selectedServerId = serverId || activeServers[0]?.id || "";
  const configuration = useQuery({ queryKey: [...DNS_POLICY_CONFIGURATION_QUERY_KEY, selectedServerId], queryFn: () => getDnsPolicyConfiguration(selectedServerId), enabled: Boolean(selectedServerId) });

  const mutation = useMutation({
    mutationFn: (request: Omit<DnsPolicyMutationInput, "expectedStateToken">) => mutateDnsPolicyConfiguration(selectedServerId, { ...request, expectedStateToken: configuration.data!.stateToken }),
    onSuccess: async (result) => {
      setError(null); setPendingDelete(null);
      if (result.configuration) queryClient.setQueryData([...DNS_POLICY_CONFIGURATION_QUERY_KEY, selectedServerId], result.configuration);
      else await queryClient.invalidateQueries({ queryKey: [...DNS_POLICY_CONFIGURATION_QUERY_KEY, selectedServerId] });
      toast.success(t("policies.updated"));
    },
    onError: (failure) => { setPendingDelete(null); setError(getApiErrorMessage(failure, t("policies.operationFailed"))); },
  });
  const criterion = (value: string) => split(value).length ? { operator: policy.matchOperator, values: split(value) } : null;
  const transferCriterion = (value: string) => split(value).length ? { operator: transferPolicy.matchOperator, values: split(value) } : null;
  const zoneScopeWeights = split(policy.zoneScopes).map((entry) => { const [name, rawWeight] = entry.split(":"); return { name: name.trim(), weight: Number(rawWeight || 1) }; });

  return <section className="space-y-4">
    <PageHeader title={t("policies.title")} description={t("policies.description")} actions={selectedServerId ? <Button variant="outline" disabled={configuration.isFetching} onClick={() => configuration.refetch()}>{t("common:actions.refresh")}</Button> : null} />
    <FormError message={error ?? (servers.isError || configuration.isError ? t("policies.loadFailed") : null)} />
    <SectionCard title={t("policies.serverTitle")} description={t("policies.liveNotice")}>
      {servers.isLoading ? <LoadingState /> : <div className="max-w-xl space-y-2"><Label htmlFor="dns-policy-server">{t("policies.fields.server")}</Label><Select id="dns-policy-server" value={selectedServerId} disabled={mutation.isPending} onChange={(e) => { setServerId(e.target.value); setError(null); }}><option value="">{t("policies.selectServer")}</option>{activeServers.map((x) => <option key={x.id} value={x.id}>{x.displayName} ({x.hostName})</option>)}</Select></div>}
    </SectionCard>
    {configuration.isLoading ? <LoadingState /> : null}
    {configuration.data ? <>
      <SectionCard title={t("policies.subnetsTitle")} description={t("policies.subnetsDescription")}>
        <div className="grid gap-3 md:grid-cols-3"><div className="space-y-2"><Label htmlFor="dns-subnet-name">{t("policies.fields.name")}</Label><Input id="dns-subnet-name" value={subnet.name} onChange={(e) => setSubnet({ ...subnet, name: e.target.value })} /></div><div className="space-y-2"><Label htmlFor="dns-subnet-ipv4">{t("policies.fields.ipv4")}</Label><Input id="dns-subnet-ipv4" placeholder="10.0.0.0/24" value={subnet.ipv4} onChange={(e) => setSubnet({ ...subnet, ipv4: e.target.value })} /></div><div className="space-y-2"><Label htmlFor="dns-subnet-ipv6">{t("policies.fields.ipv6")}</Label><Input id="dns-subnet-ipv6" placeholder="2001:db8::/64" value={subnet.ipv6} onChange={(e) => setSubnet({ ...subnet, ipv6: e.target.value })} /></div></div>
        <div className="mt-3 flex justify-end"><Button disabled={mutation.isPending || !subnet.name || (!subnet.ipv4 && !subnet.ipv6)} onClick={() => mutation.mutate({ action: "SaveClientSubnet", name: subnet.name, ipv4Subnets: split(subnet.ipv4), ipv6Subnets: split(subnet.ipv6) })}>{t("common:actions.save")}</Button></div>
        <div className="mt-4 overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left"><th className="p-2">{t("policies.fields.name")}</th><th className="p-2">IPv4</th><th className="p-2">IPv6</th><th /></tr></thead><tbody>{configuration.data.clientSubnets.map((x) => <tr key={x.name} className="border-b"><td className="p-2 font-medium">{x.name}</td><td className="p-2 font-mono text-xs">{x.ipv4Subnets.join(", ") || "-"}</td><td className="p-2 font-mono text-xs">{x.ipv6Subnets.join(", ") || "-"}</td><td className="p-2 text-right"><Button size="sm" variant="destructive" onClick={() => setPendingDelete({ action: "DeleteClientSubnet", name: x.name })}>{t("common:actions.delete")}</Button></td></tr>)}</tbody></table></div>
      </SectionCard>
      <SectionCard title={t("policies.scopesTitle")} description={t("policies.scopesDescription")}>
        <div className="grid gap-3 md:grid-cols-2"><div className="space-y-2"><Label htmlFor="dns-scope-zone">{t("policies.fields.zone")}</Label><Input id="dns-scope-zone" value={scope.zoneName} onChange={(e) => setScope({ ...scope, zoneName: e.target.value })} /></div><div className="space-y-2"><Label htmlFor="dns-scope-name">{t("policies.fields.scope")}</Label><Input id="dns-scope-name" value={scope.name} onChange={(e) => setScope({ ...scope, name: e.target.value })} /></div></div>
        <div className="mt-3 flex justify-end"><Button disabled={mutation.isPending || !scope.zoneName || !scope.name} onClick={() => mutation.mutate({ action: "CreateZoneScope", name: scope.name, zoneName: scope.zoneName })}>{t("policies.createScope")}</Button></div>
        <div className="mt-4 grid gap-2 md:grid-cols-2">{configuration.data.zoneScopes.map((x) => <div key={`${x.zoneName}/${x.name}`} className="flex items-center justify-between rounded-md border p-3"><span><strong>{x.name}</strong><span className="ml-2 text-muted-foreground">{x.zoneName}</span></span><Button size="sm" variant="destructive" onClick={() => setPendingDelete({ action: "DeleteZoneScope", name: x.name, zoneName: x.zoneName })}>{t("common:actions.delete")}</Button></div>)}</div>
      </SectionCard>
      <SectionCard title={t("policies.queryTitle")} description={t("policies.queryDescription")}>
        <div className="grid gap-3 md:grid-cols-4">
          <Field label={t("policies.fields.name")} value={policy.name} onChange={(name) => setPolicy({ ...policy, name })} />
          <div className="space-y-2"><Label htmlFor="dns-policy-level">{t("policies.fields.level")}</Label><Select id="dns-policy-level" value={policy.level} onChange={(e) => { const level = e.target.value as "Server" | "Zone"; setPolicy({ ...policy, level, decision: level === "Server" && policy.decision === "Allow" ? "Deny" : policy.decision, zoneScopes: level === "Server" ? "" : policy.zoneScopes }); }}><option value="Server">Server</option><option value="Zone">Zone</option></Select></div>
          <Field label={t("policies.fields.zone")} value={policy.zoneName} disabled={policy.level === "Server"} onChange={(zoneName) => setPolicy({ ...policy, zoneName })} />
          <div className="space-y-2"><Label htmlFor="dns-policy-action">{t("policies.fields.action")}</Label><Select id="dns-policy-action" value={policy.decision} onChange={(e) => { const decision = e.target.value as typeof policy.decision; setPolicy({ ...policy, decision, zoneScopes: decision === "Allow" ? policy.zoneScopes : "" }); }}>{policy.level === "Zone" ? <option value="Allow">Allow</option> : null}<option value="Deny">Deny</option><option value="Ignore">Ignore</option></Select></div>
          <div className="space-y-2"><Label htmlFor="dns-policy-condition">{t("policies.fields.condition")}</Label><Select id="dns-policy-condition" value={policy.condition} onChange={(e) => setPolicy({ ...policy, condition: e.target.value as "And" | "Or" })}><option value="And">AND</option><option value="Or">OR</option></Select></div>
          <div className="space-y-2"><Label htmlFor="dns-policy-operator">{t("policies.fields.operator")}</Label><Select id="dns-policy-operator" value={policy.matchOperator} onChange={(e) => setPolicy({ ...policy, matchOperator: e.target.value as "Eq" | "Ne" })}><option value="Eq">EQ</option><option value="Ne">NE</option></Select></div>
          <div className="space-y-2"><Label htmlFor="dns-policy-order">{t("policies.fields.order")}</Label><Input id="dns-policy-order" type="number" min="1" max="100000" value={policy.order} onChange={(e) => setPolicy({ ...policy, order: Number(e.target.value) })} /></div><div />
          <Field label={t("policies.fields.clientSubnets")} value={policy.clientSubnets} onChange={(clientSubnets) => setPolicy({ ...policy, clientSubnets })} />
          <Field label={t("policies.fields.fqdns")} value={policy.fqdns} onChange={(fqdns) => setPolicy({ ...policy, fqdns })} />
          <Field label={t("policies.fields.queryTypes")} value={policy.queryTypes} placeholder="A,AAAA,CNAME" onChange={(queryTypes) => setPolicy({ ...policy, queryTypes })} />
          <Field label={t("policies.fields.transportProtocols")} value={policy.transportProtocols} placeholder="TCP,UDP" onChange={(transportProtocols) => setPolicy({ ...policy, transportProtocols })} />
          <Field label={t("policies.fields.internetProtocols")} value={policy.internetProtocols} placeholder="IPv4,IPv6" onChange={(internetProtocols) => setPolicy({ ...policy, internetProtocols })} />
          <Field label={t("policies.fields.serverInterfaces")} value={policy.serverInterfaces} placeholder="10.0.0.10" onChange={(serverInterfaces) => setPolicy({ ...policy, serverInterfaces })} />
          <Field label={t("policies.fields.zoneScopes")} value={policy.zoneScopes} disabled={policy.level !== "Zone" || policy.decision !== "Allow"} placeholder="Internal:1,Public:1" onChange={(zoneScopes) => setPolicy({ ...policy, zoneScopes })} />
        </div>
        <div className="mt-3 flex justify-end"><Button disabled={mutation.isPending || !policy.name || (policy.level === "Zone" && !policy.zoneName) || (!policy.clientSubnets && !policy.fqdns && !policy.queryTypes && !policy.transportProtocols && !policy.internetProtocols && !policy.serverInterfaces)} onClick={() => mutation.mutate({ action: "SaveQueryPolicy", name: policy.name, level: policy.level, zoneName: policy.level === "Zone" ? policy.zoneName : null, decision: policy.decision, condition: policy.condition, processingOrder: policy.order, enabled: true, clientSubnet: criterion(policy.clientSubnets), fqdn: criterion(policy.fqdns), queryType: criterion(policy.queryTypes), transportProtocol: criterion(policy.transportProtocols), internetProtocol: criterion(policy.internetProtocols), serverInterfaceIp: criterion(policy.serverInterfaces), zoneScopes: zoneScopeWeights })}>{t("common:actions.save")}</Button></div>
        <div className="mt-4 space-y-2">{configuration.data.queryPolicies.map((x) => <div key={`${x.level}/${x.zoneName}/${x.name}`} className="flex flex-wrap items-center gap-3 rounded-md border p-3"><div className="min-w-48 flex-1"><div className="flex items-center gap-2"><strong>{x.name}</strong><Badge variant={x.enabled ? "success" : "secondary"}>{x.enabled ? t("policies.enabled") : t("policies.disabled")}</Badge></div><p className="text-xs text-muted-foreground">{x.level}{x.zoneName ? ` · ${x.zoneName}` : ""} · {x.action} · #{x.processingOrder}</p><p className="mt-1 font-mono text-xs">{[x.clientSubnet, x.fqdn, x.queryType, x.zoneScope].filter(Boolean).join(" | ")}</p></div><Button size="sm" variant="outline" onClick={() => mutation.mutate({ action: "SetQueryPolicyEnabled", name: x.name, level: x.level, zoneName: x.zoneName, enabled: !x.enabled })}>{x.enabled ? t("policies.disable") : t("policies.enable")}</Button><Button size="sm" variant="destructive" onClick={() => setPendingDelete({ action: "DeleteQueryPolicy", name: x.name, level: x.level, zoneName: x.zoneName })}>{t("common:actions.delete")}</Button></div>)}</div>
      </SectionCard>
      <SectionCard title={t("policies.transferTitle")} description={t("policies.transferDescription")}>
        <div className="mb-4 rounded-lg border border-amber-500/40 bg-amber-500/10 p-4 text-sm">{t("policies.transferWarning")}</div>
        <div className="grid gap-3 md:grid-cols-4">
          <Field label={t("policies.fields.name")} value={transferPolicy.name} onChange={(name) => setTransferPolicy({ ...transferPolicy, name })} />
          <div className="space-y-2"><Label htmlFor="dns-transfer-policy-level">{t("policies.fields.level")}</Label><Select id="dns-transfer-policy-level" value={transferPolicy.level} onChange={(e) => setTransferPolicy({ ...transferPolicy, level: e.target.value as "Server" | "Zone" })}><option value="Server">Server</option><option value="Zone">Zone</option></Select></div>
          <Field label={t("policies.fields.zone")} value={transferPolicy.zoneName} disabled={transferPolicy.level === "Server"} onChange={(zoneName) => setTransferPolicy({ ...transferPolicy, zoneName })} />
          <div className="space-y-2"><Label htmlFor="dns-transfer-policy-action">{t("policies.fields.action")}</Label><Select id="dns-transfer-policy-action" value={transferPolicy.decision} onChange={(e) => setTransferPolicy({ ...transferPolicy, decision: e.target.value as "Deny" | "Ignore" })}><option value="Deny">Deny</option><option value="Ignore">Ignore</option></Select></div>
          <div className="space-y-2"><Label htmlFor="dns-transfer-policy-condition">{t("policies.fields.condition")}</Label><Select id="dns-transfer-policy-condition" value={transferPolicy.condition} onChange={(e) => setTransferPolicy({ ...transferPolicy, condition: e.target.value as "And" | "Or" })}><option value="And">AND</option><option value="Or">OR</option></Select></div>
          <div className="space-y-2"><Label htmlFor="dns-transfer-policy-operator">{t("policies.fields.operator")}</Label><Select id="dns-transfer-policy-operator" value={transferPolicy.matchOperator} onChange={(e) => setTransferPolicy({ ...transferPolicy, matchOperator: e.target.value as "Eq" | "Ne" })}><option value="Eq">EQ</option><option value="Ne">NE</option></Select></div>
          <div className="space-y-2"><Label htmlFor="dns-transfer-policy-order">{t("policies.fields.order")}</Label><Input id="dns-transfer-policy-order" type="number" min="1" max="100000" value={transferPolicy.order} onChange={(e) => setTransferPolicy({ ...transferPolicy, order: Number(e.target.value) })} /></div><div />
          <Field label={t("policies.fields.clientSubnets")} value={transferPolicy.clientSubnets} onChange={(clientSubnets) => setTransferPolicy({ ...transferPolicy, clientSubnets })} />
          <Field label={t("policies.fields.transportProtocols")} value={transferPolicy.transportProtocols} placeholder="TCP,UDP" onChange={(transportProtocols) => setTransferPolicy({ ...transferPolicy, transportProtocols })} />
          <Field label={t("policies.fields.internetProtocols")} value={transferPolicy.internetProtocols} placeholder="IPv4,IPv6" onChange={(internetProtocols) => setTransferPolicy({ ...transferPolicy, internetProtocols })} />
          <Field label={t("policies.fields.serverInterfaces")} value={transferPolicy.serverInterfaces} placeholder="10.0.0.10" onChange={(serverInterfaces) => setTransferPolicy({ ...transferPolicy, serverInterfaces })} />
          <Field label={t("policies.fields.timeOfDay")} value={transferPolicy.timeOfDay} placeholder="01:00-05:00" onChange={(timeOfDay) => setTransferPolicy({ ...transferPolicy, timeOfDay })} />
        </div>
        <div className="mt-3 flex justify-end"><Button disabled={mutation.isPending || !transferPolicy.name || (transferPolicy.level === "Zone" && !transferPolicy.zoneName) || (!transferPolicy.clientSubnets && !transferPolicy.transportProtocols && !transferPolicy.internetProtocols && !transferPolicy.serverInterfaces && !transferPolicy.timeOfDay)} onClick={() => mutation.mutate({ action: "SaveZoneTransferPolicy", name: transferPolicy.name, level: transferPolicy.level, zoneName: transferPolicy.level === "Zone" ? transferPolicy.zoneName : null, decision: transferPolicy.decision, condition: transferPolicy.condition, processingOrder: transferPolicy.order, enabled: true, clientSubnet: transferCriterion(transferPolicy.clientSubnets), transportProtocol: transferCriterion(transferPolicy.transportProtocols), internetProtocol: transferCriterion(transferPolicy.internetProtocols), serverInterfaceIp: transferCriterion(transferPolicy.serverInterfaces), timeOfDay: transferCriterion(transferPolicy.timeOfDay) })}>{t("common:actions.save")}</Button></div>
        <div className="mt-4 space-y-2">{configuration.data.zoneTransferPolicies.map((x) => <div key={`${x.level}/${x.zoneName}/${x.name}`} className="flex flex-wrap items-center gap-3 rounded-md border p-3"><div className="min-w-48 flex-1"><div className="flex items-center gap-2"><strong>{x.name}</strong><Badge variant={x.enabled ? "success" : "secondary"}>{x.enabled ? t("policies.enabled") : t("policies.disabled")}</Badge></div><p className="text-xs text-muted-foreground">{x.level}{x.zoneName ? ` · ${x.zoneName}` : ""} · {x.action} · #{x.processingOrder}</p><p className="mt-1 font-mono text-xs">{[x.clientSubnet, x.transportProtocol, x.internetProtocol, x.serverInterfaceIp, x.timeOfDay].filter(Boolean).join(" | ")}</p></div><Button size="sm" variant="outline" onClick={() => mutation.mutate({ action: "SetZoneTransferPolicyEnabled", name: x.name, level: x.level, zoneName: x.zoneName, enabled: !x.enabled })}>{x.enabled ? t("policies.disable") : t("policies.enable")}</Button><Button size="sm" variant="destructive" onClick={() => setPendingDelete({ action: "DeleteZoneTransferPolicy", name: x.name, level: x.level, zoneName: x.zoneName })}>{t("common:actions.delete")}</Button></div>)}</div>
      </SectionCard>
    </> : null}
    <ConfirmDialog open={Boolean(pendingDelete)} title={t("policies.deleteTitle")} description={t("policies.deleteDescription", { name: pendingDelete?.name })} confirmText={t("common:actions.delete")} cancelText={t("common:actions.cancel")} variant="danger" isLoading={mutation.isPending} onOpenChange={(open) => { if (!open) setPendingDelete(null); }} onConfirm={() => { if (pendingDelete) mutation.mutate(pendingDelete); }} />
  </section>;
}

function Field({ label, value, onChange, placeholder, disabled }: { label: string; value: string; onChange: (value: string) => void; placeholder?: string; disabled?: boolean }) {
  const id = useId();
  return <div className="space-y-2"><Label htmlFor={id}>{label}</Label><Input id={id} value={value} disabled={disabled} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} /></div>;
}
