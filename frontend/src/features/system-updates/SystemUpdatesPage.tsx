import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, CheckCircle2, RefreshCw, ServerCog } from "lucide-react";
import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DateTimeText } from "@/components/common/DateTimeText";
import { LoadingState } from "@/components/common/LoadingState";
import { PageContainer } from "@/components/common/PageContainer";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/features/auth/auth-store";
import {
  checkForSystemUpdates,
  getSystemUpdateStatus,
  installSystemUpdate,
  SYSTEM_UPDATE_STATUS_QUERY_KEY,
} from "@/features/system-updates/api";
import { PermissionCodes } from "@/lib/permission-codes";
import { canAccess } from "@/lib/permissions";

const ACTIVE_PHASES = new Set(["Pulling", "Building", "Migrating", "Activating"]);

export function SystemUpdatesPage() {
  const { t } = useTranslation(["systemUpdates", "common"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const canManage = canAccess(currentUser, PermissionCodes.SystemUpdates.Manage);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [backupConfirmed, setBackupConfirmed] = useState(false);

  const statusQuery = useQuery({
    queryKey: SYSTEM_UPDATE_STATUS_QUERY_KEY,
    queryFn: getSystemUpdateStatus,
    refetchInterval: (query) => {
      const phase = query.state.data?.operation?.phase;
      return phase && ACTIVE_PHASES.has(phase) ? 2_000 : false;
    },
    retry: 3,
    retryDelay: 2_000,
  });

  const checkMutation = useMutation({
    mutationFn: checkForSystemUpdates,
    onSuccess: (status) => queryClient.setQueryData(SYSTEM_UPDATE_STATUS_QUERY_KEY, status),
  });

  const installMutation = useMutation({
    mutationFn: installSystemUpdate,
    onSuccess: async () => {
      setConfirmOpen(false);
      setBackupConfirmed(false);
      await queryClient.invalidateQueries({ queryKey: SYSTEM_UPDATE_STATUS_QUERY_KEY });
    },
  });

  const status = statusQuery.data;
  const phase = status?.operation?.phase;
  const isRunning = Boolean(phase && ACTIVE_PHASES.has(phase));
  const requiresReview = phase === "RequiresOperatorReview";

  useEffect(() => {
    const operationId = status?.operation?.operationId;
    if (phase === "Completed" && operationId) {
      const reloadKey = "itadmin.system-update.reloaded-operation";
      if (window.sessionStorage.getItem(reloadKey) === operationId) return;

      window.sessionStorage.setItem(reloadKey, operationId);
      const timer = window.setTimeout(() => window.location.reload(), 1_500);
      return () => window.clearTimeout(timer);
    }
  }, [phase, status?.operation?.operationId]);

  if (statusQuery.isLoading && !status) {
    return <LoadingState />;
  }

  return (
    <PageContainer variant="wide">
      <div className="space-y-6">
        <PageHeader
          title={t("systemUpdates:page.title")}
          description={t("systemUpdates:page.description")}
          actions={
            <Button
              variant="outline"
              onClick={() => checkMutation.mutate()}
              disabled={checkMutation.isPending || isRunning}
            >
              <RefreshCw className={checkMutation.isPending ? "animate-spin" : ""} />
              {t("systemUpdates:actions.check")}
            </Button>
          }
        />

        {statusQuery.isError || checkMutation.isError ? (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertTitle>{t("systemUpdates:connection.interruptedTitle")}</AlertTitle>
            <AlertDescription>{t("systemUpdates:connection.interruptedDescription")}</AlertDescription>
          </Alert>
        ) : null}

        {requiresReview ? (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertTitle>{t("systemUpdates:review.title")}</AlertTitle>
            <AlertDescription>{t("systemUpdates:review.description")}</AlertDescription>
          </Alert>
        ) : null}

        <div className="grid gap-4 lg:grid-cols-2">
          <SectionCard title={t("systemUpdates:host.title")}>
            <dl className="grid gap-4 sm:grid-cols-2">
              <StatusRow
                label={t("systemUpdates:host.agent")}
                value={status?.agentAvailable ? t("systemUpdates:status.available") : t("systemUpdates:status.unavailable")}
                success={status?.agentAvailable === true}
              />
              <StatusRow
                label={t("systemUpdates:host.repository")}
                value={t(`systemUpdates:repositoryStatuses.${status?.repositoryStatus ?? "Unknown"}`, {
                  defaultValue: t("systemUpdates:repositoryStatuses.Unknown"),
                })}
                success={status?.repositoryAccessible === true}
              />
              <StatusRow label={t("systemUpdates:host.installationPhase")} value={status?.installationPhase ?? "-"} />
              <StatusRow
                label={t("systemUpdates:host.health")}
                value={status?.healthy ? t("systemUpdates:status.healthy") : t("systemUpdates:status.unhealthy")}
                success={status?.healthy === true}
              />
            </dl>
            <p className="mt-4 text-sm text-muted-foreground">{status?.message}</p>
          </SectionCard>

          <SectionCard title={t("systemUpdates:release.title")}>
            <dl className="grid gap-4 sm:grid-cols-2">
              <StatusRow label={t("systemUpdates:release.branch")} value={status?.branch ?? "-"} />
              <StatusRow label={t("systemUpdates:release.installed")} value={status?.activeCommit ?? "-"} />
              <StatusRow label={t("systemUpdates:release.latest")} value={status?.latestCommit ?? "-"} />
              <StatusRow label={t("systemUpdates:release.previous")} value={status?.previousCommit ?? "-"} />
              <StatusRow
                label={t("systemUpdates:release.builtAt")}
                value={status?.builtAtUtc ? <DateTimeText value={status.builtAtUtc} /> : "-"}
              />
              <StatusRow
                label={t("systemUpdates:release.checkedAt")}
                value={status?.checkedAtUtc ? <DateTimeText value={status.checkedAtUtc} /> : "-"}
              />
            </dl>
            {status?.latestSubject ? (
              <p className="mt-3 text-sm text-muted-foreground">{status.latestSubject}</p>
            ) : null}
          </SectionCard>
        </div>

        <SectionCard
          title={t("systemUpdates:operation.title")}
          actions={
            status?.updateAvailable && canManage && !requiresReview ? (
              <Button onClick={() => setConfirmOpen(true)} disabled={isRunning}>
                <ServerCog />
                {t("systemUpdates:actions.install", { count: status.commitsBehind })}
              </Button>
            ) : null
          }
        >
          {status?.operation ? (
            <div className="space-y-3">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant={phase === "Failed" || requiresReview ? "destructive" : phase === "Completed" ? "success" : "secondary"}>
                  {t(`systemUpdates:phases.${phase}`)}
                </Badge>
                {status.operation.targetCommit ? <span className="text-sm">{status.operation.targetCommit}</span> : null}
              </div>
              <p className="text-sm text-muted-foreground">{status.operation.message}</p>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              {status?.updateAvailable
                ? t("systemUpdates:operation.available", { count: status.commitsBehind })
                : t("systemUpdates:operation.upToDate")}
            </p>
          )}
        </SectionCard>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={(open) => {
          setConfirmOpen(open);
          if (!open) setBackupConfirmed(false);
        }}
        title={t("systemUpdates:confirm.title", { commit: status?.latestCommit })}
        description={t("systemUpdates:confirm.description")}
        content={
          <CheckboxField
            id="system-update-backup-confirmed"
            checked={backupConfirmed}
            onCheckedChange={setBackupConfirmed}
            label={t("systemUpdates:confirm.backupLabel")}
            description={t("systemUpdates:confirm.backupDescription")}
          />
        }
        confirmText={t("systemUpdates:confirm.submit")}
        cancelText={t("common:actions.cancel")}
        confirmDisabled={!backupConfirmed}
        isLoading={installMutation.isPending}
        onConfirm={() => installMutation.mutate()}
      />
    </PageContainer>
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
