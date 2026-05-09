import { AlertTriangle, RefreshCw } from "lucide-react";

import { DateTimeText } from "@/components/common/DateTimeText";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import type { ReadinessResponse } from "@/features/health/types";
import { cn } from "@/lib/utils";
import { useTranslation } from "react-i18next";

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

  return (
    <div
      className={cn(
        "flex w-full justify-center",
        compact ? "py-4" : "min-h-[50vh] items-center py-10",
      )}
    >
      <Card
        className={cn(
          "border-border/80 shadow-sm",
          compact ? "w-full max-w-lg" : "w-full max-w-xl",
        )}
      >
        <CardHeader className="space-y-2">
          <div className="flex items-start gap-3">
            <div className="mt-0.5 rounded-md bg-destructive/10 p-2 text-destructive">
              <AlertTriangle className="size-5 shrink-0" aria-hidden />
            </div>
            <div className="min-w-0 space-y-1">
              <CardTitle className="text-lg leading-tight">
                {t("common:serviceUnavailable.title")}
              </CardTitle>
              <CardDescription className="text-pretty">
                {t("common:serviceUnavailable.description")}
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <ul className="space-y-2 text-sm text-muted-foreground">
            {!readiness.apiAvailable ? (
              <li className="flex gap-2">
                <span className="text-foreground">•</span>
                <span>{t("common:serviceUnavailable.apiUnavailable")}</span>
              </li>
            ) : null}
            {readiness.apiAvailable && !readiness.databaseAvailable ? (
              <li className="flex gap-2">
                <span className="text-foreground">•</span>
                <span>{t("common:serviceUnavailable.databaseUnavailable")}</span>
              </li>
            ) : null}
          </ul>

          {readiness.checkedAt ? (
            <p className="text-xs text-muted-foreground">
              {t("common:serviceUnavailable.lastChecked")}:{" "}
              <DateTimeText value={readiness.checkedAt} />
            </p>
          ) : null}

          {onRetry ? (
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
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}
