import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { CheckboxField } from "@/components/common/CheckboxField";
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
  DNS_NETWORK_CONFIGURATION_QUERY_KEY,
  DNS_SERVERS_QUERY_KEY,
  getDnsNetworkConfiguration,
  getDnsServers,
  mutateDnsNetworkConfiguration,
} from "./api";
import type { DnsNetworkMutationInput, DnsRootHint } from "./types";

type PendingAction = Omit<DnsNetworkMutationInput, "expectedStateToken">;
type RootHintDraft = { originalName?: string; nameServer: string; ipAddresses: string };

export function DnsNetworkConfigurationPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const queryClient = useQueryClient();
  const [serverId, setServerId] = useState("");
  const [listeningDraft, setListeningDraft] = useState<string[] | null>(null);
  const [rootHintDraft, setRootHintDraft] = useState<RootHintDraft | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [error, setError] = useState<string | null>(null);

  const servers = useQuery({ queryKey: DNS_SERVERS_QUERY_KEY, queryFn: getDnsServers });
  const activeServers = useMemo(() => servers.data?.filter((server) => server.isEnabled) ?? [], [servers.data]);
  const selectedServerId = serverId || activeServers[0]?.id || "";
  const configuration = useQuery({
    queryKey: [...DNS_NETWORK_CONFIGURATION_QUERY_KEY, selectedServerId],
    queryFn: () => getDnsNetworkConfiguration(selectedServerId),
    enabled: Boolean(selectedServerId),
  });
  const listening = listeningDraft ?? configuration.data?.listeningIpAddresses ?? [];
  const hintAddresses = rootHintDraft?.ipAddresses.split(/[\r\n,;]+/).map((value) => value.trim()).filter(Boolean) ?? [];
  const rootHintValid = Boolean(rootHintDraft?.nameServer.trim()) && hintAddresses.length > 0 && hintAddresses.length <= 16;

  const mutation = useMutation({
    mutationFn: (action: PendingAction) => mutateDnsNetworkConfiguration(selectedServerId, {
      ...action,
      expectedStateToken: configuration.data!.stateToken,
    }),
    onSuccess: async (result) => {
      setPendingAction(null);
      setListeningDraft(null);
      setRootHintDraft(null);
      setError(null);
      if (result.configuration) queryClient.setQueryData([...DNS_NETWORK_CONFIGURATION_QUERY_KEY, selectedServerId], result.configuration);
      else await queryClient.invalidateQueries({ queryKey: [...DNS_NETWORK_CONFIGURATION_QUERY_KEY, selectedServerId] });
      toast.success(t("networkConfiguration.updated"));
    },
    onError: (failure) => {
      setPendingAction(null);
      setError(getApiErrorMessage(failure, t("networkConfiguration.operationFailed")));
    },
  });

  const selectServer = (value: string) => {
    setServerId(value);
    setListeningDraft(null);
    setRootHintDraft(null);
    setError(null);
  };
  const toggleListening = (address: string, checked: boolean) => {
    const next = checked ? [...listening, address] : listening.filter((value) => value !== address);
    setListeningDraft([...new Set(next)]);
  };
  const editHint = (hint: DnsRootHint) => setRootHintDraft({
    originalName: hint.nameServer,
    nameServer: hint.nameServer,
    ipAddresses: hint.ipAddresses.join("\n"),
  });

  return <section className="space-y-4">
    <PageHeader
      title={t("networkConfiguration.title")}
      description={t("networkConfiguration.description")}
      actions={selectedServerId ? <Button variant="outline" disabled={configuration.isFetching || mutation.isPending} onClick={() => { setListeningDraft(null); setRootHintDraft(null); configuration.refetch(); }}>{t("common:actions.refresh")}</Button> : null}
    />
    <FormError message={error ?? (servers.isError || configuration.isError ? t("networkConfiguration.loadFailed") : null)} />

    <SectionCard title={t("networkConfiguration.serverTitle")} description={t("networkConfiguration.liveNotice")}>
      {servers.isLoading ? <LoadingState /> : <div className="max-w-xl space-y-2">
        <Label htmlFor="dns-network-server">{t("networkConfiguration.fields.server")}</Label>
        <Select id="dns-network-server" value={selectedServerId} disabled={mutation.isPending} onChange={(event) => selectServer(event.target.value)}>
          <option value="">{t("networkConfiguration.selectServer")}</option>
          {activeServers.map((server) => <option key={server.id} value={server.id}>{server.displayName} ({server.hostName})</option>)}
        </Select>
      </div>}
    </SectionCard>

    {configuration.isLoading ? <LoadingState /> : null}
    {configuration.data ? <>
      <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 p-4 text-sm">
        <p className="font-medium">{t("networkConfiguration.warningTitle")}</p>
        <p className="mt-1 text-muted-foreground">{t("networkConfiguration.warningDescription")}</p>
      </div>

      <SectionCard title={t("networkConfiguration.listeningTitle")} description={t("networkConfiguration.listeningDescription")}>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {configuration.data.availableIpAddresses.map((address, index) => <CheckboxField
            key={address}
            id={"dns-listen-" + index}
            label={<span className="font-mono">{address}</span>}
            checked={listening.includes(address)}
            disabled={mutation.isPending}
            onCheckedChange={(checked) => toggleListening(address, checked)}
          />)}
        </div>
        {configuration.data.availableIpAddresses.length === 0 ? <p className="text-sm text-muted-foreground">{t("networkConfiguration.noAddresses")}</p> : null}
        <div className="mt-4 flex items-center justify-between gap-3">
          <p className="text-sm text-muted-foreground">{t("networkConfiguration.selectedCount", { count: listening.length })}</p>
          <Button disabled={listening.length === 0 || mutation.isPending} onClick={() => setPendingAction({ action: "UpdateListeningAddresses", listeningIpAddresses: listening })}>{t("common:actions.save")}</Button>
        </div>
      </SectionCard>

      <SectionCard title={t("networkConfiguration.rootHintsTitle")} description={t("networkConfiguration.rootHintsDescription")}>
        <div className="mb-4 flex justify-end"><Button variant="outline" disabled={mutation.isPending} onClick={() => setRootHintDraft({ nameServer: "", ipAddresses: "" })}>{t("networkConfiguration.addHint")}</Button></div>
        <div className="space-y-2">
          {configuration.data.rootHints.map((hint) => <div key={hint.nameServer} className="flex flex-col gap-3 rounded-md border p-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0"><p className="truncate font-mono text-sm font-medium">{hint.nameServer}</p><div className="mt-1 flex flex-wrap gap-1">{hint.ipAddresses.map((address) => <Badge key={address} variant="outline" className="font-mono">{address}</Badge>)}</div></div>
            <div className="flex shrink-0 gap-2"><Button size="sm" variant="outline" disabled={mutation.isPending} onClick={() => editHint(hint)}>{t("common:actions.edit")}</Button><Button size="sm" variant="destructive" disabled={configuration.data.rootHints.length <= 1 || mutation.isPending} onClick={() => setPendingAction({ action: "RemoveRootHint", rootHintNameServer: hint.nameServer })}>{t("common:actions.delete")}</Button></div>
          </div>)}
          {configuration.data.rootHints.length === 0 ? <p className="text-sm text-muted-foreground">{t("networkConfiguration.noRootHints")}</p> : null}
        </div>

        {rootHintDraft ? <div className="mt-4 space-y-4 rounded-lg border p-4">
          <p className="font-medium">{rootHintDraft.originalName ? t("networkConfiguration.editHint") : t("networkConfiguration.addHint")}</p>
          <div className="space-y-2"><Label htmlFor="dns-root-name">{t("networkConfiguration.fields.nameServer")}</Label><Input id="dns-root-name" value={rootHintDraft.nameServer} disabled={mutation.isPending} placeholder="a.root-servers.net." onChange={(event) => setRootHintDraft({ ...rootHintDraft, nameServer: event.target.value })} /></div>
          <div className="space-y-2"><Label htmlFor="dns-root-addresses">{t("networkConfiguration.fields.ipAddresses")}</Label><Textarea id="dns-root-addresses" rows={4} value={rootHintDraft.ipAddresses} disabled={mutation.isPending} placeholder={t("networkConfiguration.ipPlaceholder")} onChange={(event) => setRootHintDraft({ ...rootHintDraft, ipAddresses: event.target.value })} /><p className="text-xs text-muted-foreground">{t("networkConfiguration.ipHelp")}</p></div>
          <div className="flex justify-end gap-2"><Button variant="outline" disabled={mutation.isPending} onClick={() => setRootHintDraft(null)}>{t("common:actions.cancel")}</Button><Button disabled={!rootHintValid || mutation.isPending} onClick={() => setPendingAction({ action: rootHintDraft.originalName ? "UpdateRootHint" : "AddRootHint", originalRootHintNameServer: rootHintDraft.originalName, rootHintNameServer: rootHintDraft.nameServer, rootHintIpAddresses: hintAddresses })}>{t("common:actions.save")}</Button></div>
        </div> : null}
      </SectionCard>
    </> : null}

    <ConfirmDialog
      open={Boolean(pendingAction)}
      title={t("networkConfiguration.confirmTitle")}
      description={pendingAction ? t("networkConfiguration.confirmations." + pendingAction.action) : undefined}
      confirmText={t("common:actions.confirm")}
      cancelText={t("common:actions.cancel")}
      variant={pendingAction?.action === "UpdateListeningAddresses" || pendingAction?.action === "RemoveRootHint" ? "danger" : "default"}
      isLoading={mutation.isPending}
      onOpenChange={(open) => { if (!open) setPendingAction(null); }}
      onConfirm={() => { if (pendingAction) mutation.mutate(pendingAction); }}
    />
  </section>;
}
