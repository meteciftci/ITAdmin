import { useState } from "react";
import type { ChangeEvent, FormEvent, ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, CheckCircle2, Lock, ShieldOff } from "lucide-react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { CheckboxField } from "@/components/common/CheckboxField";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DateTimeText } from "@/components/common/DateTimeText";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  SYSTEM_HTTPS_STATUS_QUERY_KEY,
  configureSystemHttps,
  disableSystemHttps,
  getSystemHttpsStatus,
} from "@/features/system-https/api";
import { getApiErrorMessage } from "@/lib/api-error";

interface HttpsSettingsTabProps {
  canManage: boolean;
}

export function HttpsSettingsTab({ canManage }: HttpsSettingsTabProps) {
  const { t } = useTranslation(["systemHttps", "common"]);
  const queryClient = useQueryClient();

  const [pfx, setPfx] = useState<File | null>(null);
  const [password, setPassword] = useState("");
  const [httpsPort, setHttpsPort] = useState(443);
  const [redirect, setRedirect] = useState(true);
  const [disableOpen, setDisableOpen] = useState(false);

  const statusQuery = useQuery({
    queryKey: SYSTEM_HTTPS_STATUS_QUERY_KEY,
    queryFn: getSystemHttpsStatus,
  });

  const configureMutation = useMutation({
    mutationFn: configureSystemHttps,
    onSuccess: async (result) => {
      queryClient.setQueryData(SYSTEM_HTTPS_STATUS_QUERY_KEY, {
        agentAvailable: true,
        ...result,
      });
      setPfx(null);
      setPassword("");
      toast.success(result.message || t("systemHttps:toast.configured"));
      await queryClient.invalidateQueries({ queryKey: SYSTEM_HTTPS_STATUS_QUERY_KEY });
    },
    onError: (error) => toast.error(getApiErrorMessage(error, t("systemHttps:toast.failed"))),
  });

  const disableMutation = useMutation({
    mutationFn: disableSystemHttps,
    onSuccess: async (result) => {
      setDisableOpen(false);
      toast.success(result.message || t("systemHttps:toast.disabled"));
      await queryClient.invalidateQueries({ queryKey: SYSTEM_HTTPS_STATUS_QUERY_KEY });
    },
    onError: (error) => toast.error(getApiErrorMessage(error, t("systemHttps:toast.failed"))),
  });

  const status = statusQuery.data;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!pfx) return;
    configureMutation.mutate({ pfx, password, httpsPort, redirectHttpToHttps: redirect });
  };

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    setPfx(event.target.files?.[0] ?? null);
  };

  if (statusQuery.isLoading && !status) {
    return <LoadingState />;
  }

  return (
    <div className="space-y-6">
      <p className="text-sm text-muted-foreground">{t("systemHttps:page.description")}</p>

      {status && !status.agentAvailable ? (
        <Alert variant="destructive">
          <AlertTriangle />
          <AlertTitle>{t("systemHttps:status.agentUnavailable")}</AlertTitle>
          <AlertDescription>{status.message}</AlertDescription>
        </Alert>
      ) : null}

      <SectionCard
        title={t("systemHttps:status.title")}
        actions={
          canManage && status?.enabled ? (
            <Button variant="outline" onClick={() => setDisableOpen(true)} disabled={disableMutation.isPending}>
              <ShieldOff />
              {t("systemHttps:disable.button")}
            </Button>
          ) : null
        }
      >
        <dl className="grid gap-4 sm:grid-cols-2">
          <StatusRow
            label={t("systemHttps:status.state")}
            value={status?.enabled ? t("systemHttps:status.enabled") : t("systemHttps:status.disabled")}
            success={status?.enabled === true}
          />
          <StatusRow label={t("systemHttps:status.port")} value={status?.enabled ? status.port : "-"} />
          <StatusRow
            label={t("systemHttps:status.redirect")}
            value={
              status?.redirectHttpToHttps
                ? t("systemHttps:status.redirectOn")
                : t("systemHttps:status.redirectOff")
            }
          />
          <StatusRow
            label={t("systemHttps:status.expires")}
            value={
              status?.certificateNotAfterUtc ? (
                <DateTimeText value={status.certificateNotAfterUtc} />
              ) : (
                "-"
              )
            }
          />
        </dl>
        <p className="mt-4 break-all text-sm text-muted-foreground">
          {status?.certificateSubject ?? t("systemHttps:status.noCertificate")}
        </p>
      </SectionCard>

      {canManage ? (
        <SectionCard title={t("systemHttps:form.title")}>
          <form className="space-y-4" onSubmit={handleSubmit}>
            <div className="space-y-1">
              <Label htmlFor="system-https-pfx">{t("systemHttps:form.pfxLabel")}</Label>
              <Input
                id="system-https-pfx"
                type="file"
                accept=".pfx,.p12,application/x-pkcs12"
                onChange={handleFileChange}
              />
              <p className="text-xs text-muted-foreground">{t("systemHttps:form.pfxHint")}</p>
            </div>

            <div className="space-y-1">
              <Label htmlFor="system-https-password">{t("systemHttps:form.passwordLabel")}</Label>
              <Input
                id="system-https-password"
                type="password"
                autoComplete="off"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
              <p className="text-xs text-muted-foreground">{t("systemHttps:form.passwordHint")}</p>
            </div>

            <div className="space-y-1">
              <Label htmlFor="system-https-port">{t("systemHttps:form.portLabel")}</Label>
              <Input
                id="system-https-port"
                type="number"
                min={1}
                max={65535}
                className="w-32"
                value={httpsPort}
                onChange={(event) => setHttpsPort(Number(event.target.value) || 443)}
              />
            </div>

            <CheckboxField
              id="system-https-redirect"
              checked={redirect}
              onCheckedChange={setRedirect}
              label={t("systemHttps:form.redirectLabel")}
              description={t("systemHttps:form.redirectHint")}
            />

            <Button type="submit" disabled={!pfx || configureMutation.isPending}>
              <Lock />
              {t("systemHttps:form.submit")}
            </Button>
          </form>
        </SectionCard>
      ) : null}

      <ConfirmDialog
        open={disableOpen}
        onOpenChange={setDisableOpen}
        title={t("systemHttps:disable.confirmTitle")}
        description={t("systemHttps:disable.confirmDescription")}
        confirmText={t("systemHttps:disable.confirmSubmit")}
        cancelText={t("common:actions.cancel")}
        isLoading={disableMutation.isPending}
        onConfirm={() => disableMutation.mutate()}
      />
    </div>
  );
}

function StatusRow({
  label,
  value,
  success,
}: {
  label: string;
  value: ReactNode;
  success?: boolean;
}) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-1 flex items-center gap-2 text-sm font-medium">
        {success === true ? <CheckCircle2 className="size-4 text-emerald-600" /> : null}
        {success === false ? <AlertTriangle className="size-4 text-amber-600" /> : null}
        {value}
      </dd>
    </div>
  );
}
