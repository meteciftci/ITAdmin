import { RefreshCw } from "lucide-react";
import { useTranslation } from "react-i18next";

import { BlockingStateCard } from "@/components/common/BlockingStateCard";
import { DateTimeText } from "@/components/common/DateTimeText";
import { Button } from "@/components/ui/button";
import type { ReadinessResponse } from "@/features/health/types";
import { cn } from "@/lib/utils";

type ServiceUnavailableStateProps = {
  readiness: ReadinessResponse;
  isLoading?: boolean;
  onRetry?: () => void;
  compact?: boolean;
};

export function ServiceUnavailableState({
  readiness,
  isLoading,
  onRetry,
  compact,
}: ServiceUnavailableStateProps) {
  const { t } = useTranslation(["common"]);

  const showApiIssue = !readiness.apiAvailable;
  const showDbIssue =
    readiness.apiAvailable && !readiness.databaseAvailable;
  const showLdapIssue =
    readiness.apiAvailable
    && readiness.databaseAvailable
    && !readiness.ldapAvailable;

  const details = (
    <>
      <ul className="space-y-2">
        {showApiIssue ? (
          <li className="flex gap-2">
            <span className="text-foreground">•</span>
            <span>{t("common:serviceUnavailable.apiUnavailable")}</span>
          </li>
        ) : null}
        {showDbIssue ? (
          <li className="flex gap-2">
            <span className="text-foreground">•</span>
            <span>{t("common:serviceUnavailable.databaseUnavailable")}</span>
          </li>
        ) : null}
        {showLdapIssue ? (
          <li className="flex gap-2">
            <span className="text-foreground">•</span>
            <span>{t("common:serviceUnavailable.ldapUnavailable")}</span>
          </li>
        ) : null}
      </ul>

      {readiness.checkedAt ? (
        <p className="text-xs">
          {t("common:serviceUnavailable.lastChecked")}:{" "}
          <DateTimeText value={readiness.checkedAt} />
        </p>
      ) : null}
    </>
  );

  const actions = onRetry ? (
    <Button
      type="button"
      variant="secondary"
      size={compact ? "sm" : "default"}
      className="w-full sm:w-auto"
      disabled={isLoading}
      onClick={() => onRetry()}
    >
      <RefreshCw
        className={cn("mr-2 size-4", isLoading && "animate-spin")}
        aria-hidden
      />
      {t("common:serviceUnavailable.retry")}
    </Button>
  ) : undefined;

  return (
    <BlockingStateCard
      variant="danger"
      size={compact ? "compact" : "default"}
      centered
      title={t("common:serviceUnavailable.title")}
      description={t("common:serviceUnavailable.description")}
      details={details}
      actions={actions}
    />
  );
}
