import { useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import type {
  AdDeletedObjectRestoreReadinessCheck,
  AdDeletedObjectRestoreReadinessResult,
  AdDeletedObjectRestoreReadinessStatus,
} from "@/features/ad-management/types";
import { cn } from "@/lib/utils";

type Props = {
  result: AdDeletedObjectRestoreReadinessResult;
  showSettingsLink?: boolean;
  showAllChecks?: boolean;
  onRetry?: () => void;
  isRetrying?: boolean;
  className?: string;
};

function statusContainerClass(status: AdDeletedObjectRestoreReadinessStatus): string {
  switch (status) {
    case "Ready":
      return "border-emerald-500/40 bg-emerald-500/10";
    case "Warning":
      return "border-amber-500/40 bg-amber-500/10";
    default:
      return "border-destructive/40 bg-destructive/10";
  }
}

function checkStatusLabelKey(status: AdDeletedObjectRestoreReadinessCheck["status"]): string {
  switch (status) {
    case "Success":
      return "adManagement:deletedObjects.restore.readiness.checkStatus.success";
    case "Warning":
      return "adManagement:deletedObjects.restore.readiness.checkStatus.warning";
    case "Failed":
      return "adManagement:deletedObjects.restore.readiness.checkStatus.failed";
    default:
      return "adManagement:deletedObjects.restore.readiness.checkStatus.notChecked";
  }
}

function ReadinessCheckRow({ check }: { check: AdDeletedObjectRestoreReadinessCheck }) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [copied, setCopied] = useState(false);

  async function handleCopyCommand() {
    if (!check.command?.trim()) {
      return;
    }

    try {
      await navigator.clipboard.writeText(check.command);
      setCopied(true);
      toast.success(t("adManagement:deletedObjects.restore.readiness.commandCopied"));
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      toast.error(t("adManagement:deletedObjects.restore.readiness.commandCopyFailed"));
    }
  }

  return (
    <div className="rounded-md border bg-card/60 p-3 space-y-2">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="font-medium text-sm">{check.title}</div>
        <span className="text-xs text-muted-foreground">
          {t(checkStatusLabelKey(check.status))}
        </span>
      </div>
      {check.message ? (
        <p className="text-sm text-muted-foreground">{check.message}</p>
      ) : null}
      {check.remediation ? (
        <p className="text-sm">
          <span className="font-medium">
            {t("adManagement:deletedObjects.restore.readiness.remediation")}:{" "}
          </span>
          <span className="text-muted-foreground">{check.remediation}</span>
        </p>
      ) : null}
      {check.command ? (
        <div className="space-y-1.5">
          <div className="text-xs font-medium text-muted-foreground">
            {t("adManagement:deletedObjects.restore.readiness.command")}
          </div>
          <div className="flex flex-wrap items-start gap-2">
            <code className="block flex-1 overflow-x-auto rounded border bg-muted/40 px-2 py-1.5 text-xs">
              {check.command}
            </code>
            <Button type="button" variant="outline" size="sm" onClick={handleCopyCommand}>
              {copied
                ? t("adManagement:deletedObjects.restore.readiness.commandCopied")
                : t("adManagement:deletedObjects.restore.readiness.copyCommand")}
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

export function AdDeletedObjectRestoreReadinessPanel({
  result,
  showSettingsLink = false,
  showAllChecks = true,
  onRetry,
  isRetrying = false,
  className,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);

  const titleKey =
    result.status === "Ready"
      ? "adManagement:deletedObjects.restore.readiness.readyTitle"
      : result.status === "Warning"
        ? "adManagement:deletedObjects.restore.readiness.warningTitle"
        : "adManagement:deletedObjects.restore.readiness.unavailableTitle";

  const reasonsToShow =
    result.status === "NotReady" && result.blockingReasons.length > 0
      ? result.blockingReasons
      : result.checks;

  return (
    <div className={cn("space-y-4", className)}>
      <div className={cn("rounded-lg border p-4 space-y-2", statusContainerClass(result.status))}>
        <h3 className="text-sm font-semibold">{t(titleKey)}</h3>
        <p className="text-sm text-muted-foreground">{result.summaryMessage}</p>
        {result.status === "NotReady" ? (
          <p className="text-sm text-muted-foreground">
            {t("adManagement:deletedObjects.restore.readiness.unavailableDescription")}
          </p>
        ) : null}
      </div>

      {result.status === "Warning" && result.warnings.length > 0 ? (
        <div className="space-y-2">
          <h4 className="text-sm font-medium">
            {t("adManagement:deletedObjects.restore.readiness.warnings")}
          </h4>
          <div className="space-y-2">
            {result.warnings.map((check) => (
              <ReadinessCheckRow key={check.key} check={check} />
            ))}
          </div>
        </div>
      ) : null}

      {result.status === "NotReady" && result.blockingReasons.length > 0 ? (
        <div className="space-y-2">
          <h4 className="text-sm font-medium">
            {t("adManagement:deletedObjects.restore.readiness.blockingReasons")}
          </h4>
          <div className="space-y-2">
            {result.blockingReasons.map((check) => (
              <ReadinessCheckRow key={check.key} check={check} />
            ))}
          </div>
        </div>
      ) : null}

      {result.status !== "NotReady" && showAllChecks && reasonsToShow.length > 0 ? (
        <div className="space-y-2">
          {result.status === "Ready" ? (
            <h4 className="text-sm font-medium">
              {t("adManagement:settings.restoreReadiness.checksTitle")}
            </h4>
          ) : null}
          <div className="space-y-2">
            {reasonsToShow.map((check) => (
              <ReadinessCheckRow key={check.key} check={check} />
            ))}
          </div>
        </div>
      ) : null}

      <div className="flex flex-wrap gap-2">
        {onRetry ? (
          <Button type="button" variant="outline" onClick={onRetry} disabled={isRetrying}>
            {t("adManagement:deletedObjects.restore.readiness.retry")}
          </Button>
        ) : null}
        {showSettingsLink ? (
          <Link
            to="/settings/modules/ad-management"
            className={cn(buttonVariants({ variant: "outline", size: "default" }))}
          >
            {t("adManagement:deletedObjects.restore.readiness.goToSettings")}
          </Link>
        ) : null}
      </div>
    </div>
  );
}
