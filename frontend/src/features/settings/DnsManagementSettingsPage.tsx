import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { CheckboxField } from "@/components/common/CheckboxField";
import { FormError } from "@/components/common/FormError";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { DNS_SETTINGS_QUERY_KEY, getDnsSettings, updateDnsSettings } from "@/features/dns-management/api";
import type { DnsManagementSettings } from "@/features/dns-management/types";
import { getApiErrorMessage } from "@/lib/api-error";

const defaults: DnsManagementSettings = { isEnabled: false, automaticSyncEnabled: true, defaultSyncIntervalMinutes: 15, healthCheckIntervalMinutes: 5, commandTimeoutSeconds: 30, maxParallelServers: 3, snapshotRetentionDays: 30, syncRecordInventory: true, promptForFullSyncOnComparisonOpen: true, comparisonSnapshotStaleAfterMinutes: 15 };

export function DnsManagementSettingsPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const queryClient = useQueryClient();
  const [form, setForm] = useState(defaults);
  const [error, setError] = useState<string | null>(null);
  const query = useQuery({ queryKey: DNS_SETTINGS_QUERY_KEY, queryFn: getDnsSettings });
  useEffect(() => {
    /* eslint-disable react-hooks/set-state-in-effect -- hydrate the settings form from the server */
    if (query.data) setForm(query.data);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [query.data]);
  const mutation = useMutation({
    mutationFn: () => updateDnsSettings(form),
    onSuccess: async () => { setError(null); await queryClient.invalidateQueries({ queryKey: DNS_SETTINGS_QUERY_KEY }); toast.success(t("settings.saved")); },
    onError: (x) => setError(getApiErrorMessage(x, t("settings.saveFailed"))),
  });
  const numberField = (key: keyof DnsManagementSettings, min: number, max: number) => (
    <div className="space-y-2"><Label htmlFor={String(key)}>{t(`settings.fields.${String(key)}`)}</Label><Input id={String(key)} type="number" min={min} max={max} value={String(form[key] ?? "")} onChange={(e) => setForm({ ...form, [key]: Number(e.target.value) })} /></div>
  );
  return <section className="space-y-4">
    <PageHeader title={t("settings.title")} description={t("settings.description")} />
    {query.isLoading ? <LoadingState /> : null}
    {query.isError ? <FormError message={getApiErrorMessage(query.error, t("settings.loadFailed"))} /> : null}
    <SectionCard title={t("settings.moduleTitle")} description={t("settings.moduleDescription")}>
      <div className="space-y-5">
        {error ? <FormError message={error} /> : null}
        <div className="grid gap-4 md:grid-cols-2">
          <CheckboxField id="dns-enabled" label={t("settings.fields.isEnabled")} checked={form.isEnabled} onCheckedChange={(x) => setForm({ ...form, isEnabled: x === true })} />
          <CheckboxField id="dns-auto-sync" label={t("settings.fields.automaticSyncEnabled")} checked={form.automaticSyncEnabled} onCheckedChange={(x) => setForm({ ...form, automaticSyncEnabled: x === true })} />
          <CheckboxField id="dns-records" label={t("settings.fields.syncRecordInventory")} checked={form.syncRecordInventory} onCheckedChange={(x) => setForm({ ...form, syncRecordInventory: x === true })} />
          <CheckboxField id="dns-prompt" label={t("settings.fields.promptForFullSyncOnComparisonOpen")} checked={form.promptForFullSyncOnComparisonOpen} onCheckedChange={(x) => setForm({ ...form, promptForFullSyncOnComparisonOpen: x === true })} />
          {numberField("defaultSyncIntervalMinutes", 1, 1440)}
          {numberField("healthCheckIntervalMinutes", 1, 1440)}
          {numberField("commandTimeoutSeconds", 5, 300)}
          {numberField("maxParallelServers", 1, 20)}
          {numberField("snapshotRetentionDays", 1, 3650)}
          {numberField("comparisonSnapshotStaleAfterMinutes", 1, 10080)}
        </div>
        <div className="flex justify-end"><Button onClick={() => mutation.mutate()} disabled={mutation.isPending || !query.data}>{t("common:actions.save")}</Button></div>
      </div>
    </SectionCard>
  </section>;
}
