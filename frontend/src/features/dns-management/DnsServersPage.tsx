import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { CheckboxField } from "@/components/common/CheckboxField";
import { FormError } from "@/components/common/FormError";
import { LoadingState } from "@/components/common/LoadingState";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { getApiErrorMessage } from "@/lib/api-error";
import { deleteDnsCredential, DNS_CREDENTIALS_QUERY_KEY, DNS_SERVERS_QUERY_KEY, getDnsCredentials, getDnsServers, saveDnsCredential, saveDnsServer } from "./api";
import type { DnsAuthenticationMode, DnsCredentialProfile, DnsServer, DnsServerEnvironment } from "./types";

const emptyCredential = { name: "", userName: "", password: "", authenticationMode: "Negotiate" as DnsAuthenticationMode, isEnabled: true };
const emptyServer = { displayName: "", hostName: "", port: 5986, environment: "Internal" as DnsServerEnvironment, credentialProfileId: "", isEnabled: true, syncIntervalMinutes: null as number | null, tlsCertificateThumbprint: "", notes: "" };

export function DnsServersPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const user = useAuthStore((x) => x.user);
  const canManage = canAccess(user, PermissionCodes.DnsManagement.Servers.Manage);
  const queryClient = useQueryClient();
  const credentials = useQuery({ queryKey: DNS_CREDENTIALS_QUERY_KEY, queryFn: getDnsCredentials, enabled: canManage });
  const servers = useQuery({ queryKey: DNS_SERVERS_QUERY_KEY, queryFn: getDnsServers });
  const [credentialId, setCredentialId] = useState<string | null>(null);
  const [credential, setCredential] = useState(emptyCredential);
  const [serverId, setServerId] = useState<string | null>(null);
  const [server, setServer] = useState(emptyServer);
  const [error, setError] = useState<string | null>(null);
  const [credentialToDelete, setCredentialToDelete] = useState<DnsCredentialProfile | null>(null);

  const credentialMutation = useMutation({ mutationFn: () => saveDnsCredential(credentialId, credential), onSuccess: async () => { setCredentialId(null); setCredential(emptyCredential); setError(null); await queryClient.invalidateQueries({ queryKey: DNS_CREDENTIALS_QUERY_KEY }); toast.success(t("messages.credentialSaved")); }, onError: (x) => setError(getApiErrorMessage(x, t("messages.saveFailed"))) });
  const serverMutation = useMutation({ mutationFn: () => saveDnsServer(serverId, { ...server, syncIntervalMinutes: server.syncIntervalMinutes || null, tlsCertificateThumbprint: server.tlsCertificateThumbprint || null, notes: server.notes || null }), onSuccess: async () => { setServerId(null); setServer(emptyServer); setError(null); await queryClient.invalidateQueries({ queryKey: DNS_SERVERS_QUERY_KEY }); toast.success(t("messages.serverSaved")); }, onError: (x) => setError(getApiErrorMessage(x, t("messages.saveFailed"))) });
  const removeCredential = useMutation({ mutationFn: deleteDnsCredential, onSuccess: async () => { setCredentialToDelete(null); await queryClient.invalidateQueries({ queryKey: DNS_CREDENTIALS_QUERY_KEY }); toast.success(t("messages.credentialDeleted")); }, onError: (x) => setError(getApiErrorMessage(x, t("messages.credentialInUse"))) });
  const editCredential = (x: DnsCredentialProfile) => { setCredentialId(x.id); setCredential({ name: x.name, userName: x.userName, password: "", authenticationMode: x.authenticationMode, isEnabled: x.isEnabled }); };
  const editServer = (x: DnsServer) => { setServerId(x.id); setServer({ displayName: x.displayName, hostName: x.hostName, port: x.port, environment: x.environment, credentialProfileId: x.credentialProfileId, isEnabled: x.isEnabled, syncIntervalMinutes: x.syncIntervalMinutes ?? null, tlsCertificateThumbprint: x.tlsCertificateThumbprint ?? "", notes: x.notes ?? "" }); };

  return <section className="space-y-4">
    <PageHeader title={t("servers.title")} description={t("servers.description")} />
    {error ? <FormError message={error} /> : null}
    {servers.isLoading ? <LoadingState /> : null}
    {servers.isError ? <FormError message={getApiErrorMessage(servers.error, t("messages.loadFailed"))} /> : null}
    {credentials.isError ? <FormError message={getApiErrorMessage(credentials.error, t("messages.loadFailed"))} /> : null}
    <SectionCard title={t("servers.listTitle")} description={t("servers.phaseNotice")}>
      <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left"><th className="p-3">{t("servers.fields.name")}</th><th className="p-3">{t("servers.fields.endpoint")}</th><th className="p-3">{t("servers.fields.environment")}</th><th className="p-3">{t("servers.fields.credential")}</th><th className="p-3">{t("servers.fields.status")}</th>{canManage ? <th className="p-3" /> : null}</tr></thead><tbody>{servers.data?.map((x) => <tr key={x.id} className="border-b last:border-0"><td className="p-3 font-medium">{x.displayName}</td><td className="p-3">{x.hostName}:{x.port}</td><td className="p-3">{t(`environments.${x.environment}`)}</td><td className="p-3">{x.credentialProfileName}</td><td className="p-3">{x.isEnabled ? t("common:status.active") : t("common:status.passive")}</td>{canManage ? <td className="p-3 text-right"><Button variant="outline" size="sm" onClick={() => editServer(x)}>{t("common:actions.edit")}</Button></td> : null}</tr>)}</tbody></table>{servers.data?.length === 0 ? <p className="p-4 text-sm text-muted-foreground">{t("servers.empty")}</p> : null}</div>
    </SectionCard>
    {canManage ? <>
      <SectionCard title={t("credentials.title")} description={t("credentials.securityNotice")}>
          <div className="space-y-4"><div className="overflow-x-auto"><table className="w-full text-sm"><tbody>{credentials.data?.map((x) => <tr key={x.id} className="border-b"><td className="p-3 font-medium">{x.name}</td><td className="p-3">{x.userName}</td><td className="p-3">{x.hasPassword ? t("credentials.secretStored") : "—"}</td><td className="p-3 text-right space-x-2"><Button variant="outline" size="sm" onClick={() => editCredential(x)}>{t("common:actions.edit")}</Button><Button variant="destructive" size="sm" onClick={() => setCredentialToDelete(x)}>{t("common:actions.delete")}</Button></td></tr>)}</tbody></table></div>
          <div className="grid gap-4 md:grid-cols-2"><div className="space-y-2"><Label>{t("credentials.fields.name")}</Label><Input value={credential.name} onChange={(e) => setCredential({ ...credential, name: e.target.value })} /></div><div className="space-y-2"><Label>{t("credentials.fields.userName")}</Label><Input value={credential.userName} onChange={(e) => setCredential({ ...credential, userName: e.target.value })} /></div><div className="space-y-2"><Label>{t("credentials.fields.authenticationMode")}</Label><Select value={credential.authenticationMode} onChange={(e) => setCredential({ ...credential, authenticationMode: e.target.value as DnsAuthenticationMode })}><option value="Negotiate">Negotiate</option><option value="BasicOverTls">Basic over TLS</option></Select></div><div className="space-y-2"><Label>{t("credentials.fields.password")}</Label><Input type="password" autoComplete="new-password" value={credential.password} placeholder={credentialId ? t("credentials.keepPassword") : ""} onChange={(e) => setCredential({ ...credential, password: e.target.value })} /></div><CheckboxField id="credential-enabled" label={t("credentials.fields.enabled")} checked={credential.isEnabled} onCheckedChange={(x) => setCredential({ ...credential, isEnabled: x === true })} /></div>
          <div className="flex justify-end gap-2">{credentialId ? <Button variant="outline" onClick={() => { setCredentialId(null); setCredential(emptyCredential); }}>{t("common:actions.cancel")}</Button> : null}<Button onClick={() => credentialMutation.mutate()} disabled={!credential.name || !credential.userName || (!credentialId && !credential.password)}>{t("common:actions.save")}</Button></div>
        </div>
      </SectionCard>
      <SectionCard title={serverId ? t("servers.editTitle") : t("servers.addTitle")}>
        <div className="space-y-4"><div className="grid gap-4 md:grid-cols-2"><div className="space-y-2"><Label>{t("servers.fields.name")}</Label><Input value={server.displayName} onChange={(e) => setServer({ ...server, displayName: e.target.value })} /></div><div className="space-y-2"><Label>{t("servers.fields.hostName")}</Label><Input value={server.hostName} onChange={(e) => setServer({ ...server, hostName: e.target.value })} /></div><div className="space-y-2"><Label>{t("servers.fields.port")}</Label><Input type="number" min="1" max="65535" value={server.port} onChange={(e) => setServer({ ...server, port: Number(e.target.value) })} /></div><div className="space-y-2"><Label>{t("servers.fields.environment")}</Label><Select value={server.environment} onChange={(e) => setServer({ ...server, environment: e.target.value as DnsServerEnvironment })}><option value="Internal">{t("environments.Internal")}</option><option value="Public">{t("environments.Public")}</option><option value="Other">{t("environments.Other")}</option></Select></div><div className="space-y-2"><Label>{t("servers.fields.credential")}</Label><Select value={server.credentialProfileId} onChange={(e) => setServer({ ...server, credentialProfileId: e.target.value })}><option value="">{t("servers.selectCredential")}</option>{credentials.data?.filter((x) => x.isEnabled).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</Select></div><div className="space-y-2"><Label>{t("servers.fields.syncInterval")}</Label><Input type="number" min="1" max="1440" value={server.syncIntervalMinutes ?? ""} onChange={(e) => setServer({ ...server, syncIntervalMinutes: e.target.value ? Number(e.target.value) : null })} /></div><div className="space-y-2 md:col-span-2"><Label>{t("servers.fields.thumbprint")}</Label><Input value={server.tlsCertificateThumbprint} onChange={(e) => setServer({ ...server, tlsCertificateThumbprint: e.target.value })} /></div><div className="space-y-2 md:col-span-2"><Label>{t("servers.fields.notes")}</Label><Textarea value={server.notes} onChange={(e) => setServer({ ...server, notes: e.target.value })} /></div><CheckboxField id="server-enabled" label={t("servers.fields.enabled")} checked={server.isEnabled} onCheckedChange={(x) => setServer({ ...server, isEnabled: x === true })} /></div><div className="flex justify-end gap-2">{serverId ? <Button variant="outline" onClick={() => { setServerId(null); setServer(emptyServer); }}>{t("common:actions.cancel")}</Button> : null}<Button onClick={() => serverMutation.mutate()} disabled={!server.displayName || !server.hostName || !server.credentialProfileId}>{t("common:actions.save")}</Button></div></div>
      </SectionCard>
      <ConfirmDialog open={credentialToDelete !== null} title={t("credentials.deleteTitle")} description={t("credentials.deleteDescription", { name: credentialToDelete?.name })} confirmText={t("common:actions.delete")} cancelText={t("common:actions.cancel")} variant="danger" isLoading={removeCredential.isPending} onOpenChange={(open) => { if (!open) setCredentialToDelete(null); }} onConfirm={() => { if (credentialToDelete) removeCredential.mutate(credentialToDelete.id); }} />
    </> : null}
  </section>;
}
