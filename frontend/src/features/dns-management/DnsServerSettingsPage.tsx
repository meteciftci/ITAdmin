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
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { useAuthStore } from "@/features/auth/auth-store";
import { getApiErrorMessage } from "@/lib/api-error";
import { PermissionCodes } from "@/lib/permission-codes";
import { canAccess } from "@/lib/permissions";
import {
  clearDnsServerCache,
  DNS_SERVER_SETTINGS_QUERY_KEY,
  DNS_SERVERS_QUERY_KEY,
  getDnsServers,
  getDnsServerSettings,
  updateDnsServerSettings,
} from "./api";
import type { DnsServerSettings } from "./types";

type SettingsForm = Omit<DnsServerSettings, "forwarderAddresses"> & { forwarderAddresses: string };

export function DnsServerSettingsPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.DnsManagement.ManageServerSettings);
  const canClearCache = canAccess(user, PermissionCodes.DnsManagement.ClearCache);
  const queryClient = useQueryClient();
  const [serverId, setServerId] = useState("");
  const [formDraft, setFormDraft] = useState<SettingsForm | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmCacheClear, setConfirmCacheClear] = useState(false);

  const servers = useQuery({ queryKey: DNS_SERVERS_QUERY_KEY, queryFn: getDnsServers });
  const activeServers = useMemo(
    () => servers.data?.filter((server) => server.isEnabled) ?? [],
    [servers.data],
  );
  const selectedServerId = serverId || activeServers[0]?.id || "";
  const settings = useQuery({
    queryKey: [...DNS_SERVER_SETTINGS_QUERY_KEY, selectedServerId],
    queryFn: () => getDnsServerSettings(selectedServerId),
    enabled: canManage && Boolean(selectedServerId),
  });
  const form = formDraft ?? (settings.data
    ? { ...settings.data, forwarderAddresses: settings.data.forwarderAddresses.join("\n") }
    : null);

  const update = useMutation({
    mutationFn: () => {
      const addresses = form!.forwarderAddresses.split(/[\n,;]+/)
        .map((value) => value.trim()).filter(Boolean);
      return updateDnsServerSettings(selectedServerId, {
        forwarderAddresses: addresses,
        forwarderUseRootHint: form!.forwarderUseRootHint,
        forwarderTimeoutSeconds: form!.forwarderTimeoutSeconds,
        forwarderEnableReordering: form!.forwarderEnableReordering,
        recursionEnabled: form!.recursionEnabled,
        recursionAdditionalTimeoutSeconds: form!.recursionAdditionalTimeoutSeconds,
        recursionRetryIntervalSeconds: form!.recursionRetryIntervalSeconds,
        recursionTimeoutSeconds: form!.recursionTimeoutSeconds,
        recursionSecureResponse: form!.recursionSecureResponse,
        expectedStateToken: form!.stateToken,
      });
    },
    onSuccess: async (result) => {
      setError(null);
      setFormDraft(null);
      if (result.settings) queryClient.setQueryData([...DNS_SERVER_SETTINGS_QUERY_KEY, selectedServerId], result.settings);
      else await queryClient.invalidateQueries({ queryKey: [...DNS_SERVER_SETTINGS_QUERY_KEY, selectedServerId] });
      toast.success(t("serverSettings.updated"));
    },
    onError: (failure) => setError(getApiErrorMessage(failure, t("serverSettings.updateFailed"))),
  });
  const clearCache = useMutation({
    mutationFn: () => clearDnsServerCache(selectedServerId),
    onSuccess: () => { setConfirmCacheClear(false); setError(null); toast.success(t("serverSettings.cacheCleared")); },
    onError: (failure) => { setConfirmCacheClear(false); setError(getApiErrorMessage(failure, t("serverSettings.cacheClearFailed"))); },
  });

  const selectServer = (value: string) => {
    setServerId(value);
    setFormDraft(null);
    setError(null);
  };

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("serverSettings.title")}
        description={t("serverSettings.description")}
        actions={canManage && selectedServerId ? <Button variant="outline" disabled={settings.isFetching} onClick={() => { setFormDraft(null); settings.refetch(); }}>{t("common:actions.refresh")}</Button> : null}
      />
      <FormError message={error ?? (servers.isError || settings.isError ? t("serverSettings.loadFailed") : null)} />

      <SectionCard title={t("serverSettings.serverTitle")} description={t("serverSettings.serverDescription")}>
        {servers.isLoading ? <LoadingState /> : (
          <div className="max-w-xl space-y-2">
            <Label htmlFor="dns-settings-server">{t("serverSettings.fields.server")}</Label>
            <Select id="dns-settings-server" value={selectedServerId} disabled={update.isPending || clearCache.isPending || confirmCacheClear} onChange={(event) => selectServer(event.target.value)}>
              <option value="">{t("serverSettings.selectServer")}</option>
              {activeServers.map((server) => <option key={server.id} value={server.id}>{server.displayName} ({server.hostName})</option>)}
            </Select>
            {servers.isSuccess && activeServers.length === 0 ? <p className="text-sm text-muted-foreground">{t("serverSettings.noServers")}</p> : null}
          </div>
        )}
      </SectionCard>

      {canManage && selectedServerId ? <>
        {settings.isLoading ? <LoadingState /> : null}
        {form ? <>
          <SectionCard title={t("serverSettings.forwardersTitle")} description={t("serverSettings.forwardersDescription")}>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="dns-forwarders">{t("serverSettings.fields.forwarders")}</Label>
                <Textarea id="dns-forwarders" rows={4} value={form.forwarderAddresses} placeholder={t("serverSettings.forwardersPlaceholder")} onChange={(event) => setFormDraft({ ...form, forwarderAddresses: event.target.value })} />
                <p className="text-xs text-muted-foreground">{t("serverSettings.forwardersHelp")}</p>
              </div>
              <div className="space-y-2"><Label htmlFor="dns-forwarder-timeout">{t("serverSettings.fields.forwarderTimeout")}</Label><Input id="dns-forwarder-timeout" type="number" min="0" max="15" value={form.forwarderTimeoutSeconds} onChange={(event) => setFormDraft({ ...form, forwarderTimeoutSeconds: Number(event.target.value) })} /></div>
              <div className="space-y-3 pt-1"><CheckboxField id="dns-root-hints" label={t("serverSettings.fields.useRootHints")} checked={form.forwarderUseRootHint} onCheckedChange={(value) => setFormDraft({ ...form, forwarderUseRootHint: value === true })} /><CheckboxField id="dns-forwarder-reordering" label={t("serverSettings.fields.enableReordering")} checked={form.forwarderEnableReordering} onCheckedChange={(value) => setFormDraft({ ...form, forwarderEnableReordering: value === true })} /></div>
            </div>
          </SectionCard>

          <SectionCard title={t("serverSettings.recursionTitle")} description={t("serverSettings.recursionDescription")}>
            <div className="grid gap-4 md:grid-cols-3">
              <CheckboxField id="dns-recursion-enabled" label={t("serverSettings.fields.recursionEnabled")} checked={form.recursionEnabled} onCheckedChange={(value) => setFormDraft({ ...form, recursionEnabled: value === true })} />
              <CheckboxField id="dns-secure-response" label={t("serverSettings.fields.secureResponse")} checked={form.recursionSecureResponse} onCheckedChange={(value) => setFormDraft({ ...form, recursionSecureResponse: value === true })} />
              <div />
              <div className="space-y-2"><Label htmlFor="dns-additional-timeout">{t("serverSettings.fields.additionalTimeout")}</Label><Input id="dns-additional-timeout" type="number" min="0" max="15" value={form.recursionAdditionalTimeoutSeconds} onChange={(event) => setFormDraft({ ...form, recursionAdditionalTimeoutSeconds: Number(event.target.value) })} /></div>
              <div className="space-y-2"><Label htmlFor="dns-retry-interval">{t("serverSettings.fields.retryInterval")}</Label><Input id="dns-retry-interval" type="number" min="1" max="15" value={form.recursionRetryIntervalSeconds} onChange={(event) => setFormDraft({ ...form, recursionRetryIntervalSeconds: Number(event.target.value) })} /></div>
              <div className="space-y-2"><Label htmlFor="dns-recursion-timeout">{t("serverSettings.fields.recursionTimeout")}</Label><Input id="dns-recursion-timeout" type="number" min="1" max="15" value={form.recursionTimeoutSeconds} onChange={(event) => setFormDraft({ ...form, recursionTimeoutSeconds: Number(event.target.value) })} /></div>
            </div>
            <div className="mt-4 flex justify-end"><Button disabled={update.isPending} onClick={() => update.mutate()}>{update.isPending ? t("serverSettings.saving") : t("common:actions.save")}</Button></div>
          </SectionCard>
        </> : null}
      </> : null}

      {canClearCache && selectedServerId ? <SectionCard title={t("serverSettings.cacheTitle")} description={t("serverSettings.cacheDescription")}>
        <div className="flex justify-end"><Button variant="destructive" disabled={clearCache.isPending} onClick={() => setConfirmCacheClear(true)}>{t("serverSettings.clearCache")}</Button></div>
      </SectionCard> : null}

      <ConfirmDialog open={confirmCacheClear} title={t("serverSettings.cacheConfirmTitle")} description={t("serverSettings.cacheConfirmDescription", { server: activeServers.find((server) => server.id === selectedServerId)?.displayName })} confirmText={t("serverSettings.clearCache")} cancelText={t("common:actions.cancel")} variant="danger" isLoading={clearCache.isPending} onOpenChange={setConfirmCacheClear} onConfirm={() => clearCache.mutate()} />
    </section>
  );
}
