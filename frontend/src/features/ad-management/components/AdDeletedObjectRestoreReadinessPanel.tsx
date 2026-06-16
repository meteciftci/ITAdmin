import { useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import type {
  AdDeletedObjectRestoreReadinessCheck,
  AdDeletedObjectRestoreReadinessResult,
  AdDeletedObjectRestoreReadinessStatus,
} from "@/features/ad-management/types";
import { translateReadinessText } from "@/features/ad-management/restore-readiness-i18n";
import { cn } from "@/lib/utils";

type Props = {
  result: AdDeletedObjectRestoreReadinessResult;
  showSettingsLink?: boolean;
  showAllChecks?: boolean;
  showRetry?: boolean;
  onRetry?: () => void;
  isRetrying?: boolean;
  className?: string;
};

function readinessStatusContainerClass(
  status: AdDeletedObjectRestoreReadinessStatus,
): string {
  switch (status) {
    case "Ready":
      return "border-emerald-500/50 bg-emerald-500/10";
    case "Warning":
      return "border-amber-500/50 bg-amber-500/10";
    default:
      return "border-destructive/50 bg-destructive/10";
  }
}

function readinessCheckRowContainerClass(
  status: AdDeletedObjectRestoreReadinessCheck["status"],
): string {
  switch (status) {
    case "Success":
      return "border-emerald-500/50 bg-emerald-500/10";
    case "Warning":
      return "border-amber-500/50 bg-amber-500/10";
    case "Failed":
      return "border-destructive/50 bg-destructive/10";
    default:
      return "border-blue-500/40 bg-blue-500/5 dark:bg-blue-500/10";
  }
}

function readinessCheckStatusBadgeVariant(
  status: AdDeletedObjectRestoreReadinessCheck["status"],
): "success" | "warning" | "destructive" | "info" {
  switch (status) {
    case "Success":
      return "success";
    case "Warning":
      return "warning";
    case "Failed":
      return "destructive";
    default:
      return "info";
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

  const title = translateReadinessText(
    t,
    check.titleKey,
    check.titleParams,
    check.title,
  );
  const message = translateReadinessText(
    t,
    check.messageKey,
    check.messageParams,
    check.message,
  );
  const remediation = translateReadinessText(
    t,
    check.remediationKey,
    check.remediationParams,
    check.remediation,
  );

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
    <div
      className={cn(
        "rounded-md border p-3 space-y-2",
        readinessCheckRowContainerClass(check.status),
      )}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="font-medium text-sm">{title}</div>
        <Badge variant={readinessCheckStatusBadgeVariant(check.status)}>
          {t(checkStatusLabelKey(check.status))}
        </Badge>
      </div>
      {message ? <p className="text-sm text-muted-foreground">{message}</p> : null}
      {remediation ? (
        <p className="text-sm">
          <span className="font-medium">
            {t("adManagement:deletedObjects.restore.readiness.remediation")}:{" "}
          </span>
          <span className="text-muted-foreground">{remediation}</span>
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
  showRetry = true,
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

  const summary = translateReadinessText(
    t,
    result.summaryKey,
    result.summaryParams,
    result.summaryMessage,
  );

  const warningKeys = new Set(result.warnings.map((check) => check.key));
  const checksToShow =
    result.status === "NotReady" && result.blockingReasons.length > 0
      ? result.blockingReasons
      : result.status === "Warning"
        ? result.checks.filter((check) => !warningKeys.has(check.key))
        : result.checks;

  return (
    <div className={cn("space-y-4", className)}>
      <div
        className={cn(
          "rounded-lg border p-4 space-y-2",
          readinessStatusContainerClass(result.status),
        )}
      >
        <h3 className="text-sm font-semibold">{t(titleKey)}</h3>
        {summary ? <p className="text-sm text-muted-foreground">{summary}</p> : null}
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

      {result.status !== "NotReady" && showAllChecks && checksToShow.length > 0 ? (
        <div className="space-y-2">
          {result.status === "Ready" ? (
            <h4 className="text-sm font-medium">
              {t("adManagement:settings.restoreReadiness.checksTitle")}
            </h4>
          ) : null}
          <div className="space-y-2">
            {checksToShow.map((check) => (
              <ReadinessCheckRow key={check.key} check={check} />
            ))}
          </div>
        </div>
      ) : null}

      <div className="flex flex-wrap gap-2">
        {showRetry && onRetry ? (
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
