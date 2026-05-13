import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { getApiErrorInfo } from "@/lib/api-error";
import { cn } from "@/lib/utils";

type ApiErrorStateProps = {
  error: unknown;
  fallbackTitle?: string;
  fallbackDescription?: string;
  retry?: ReactNode;
  compact?: boolean;
};

export function ApiErrorState({
  error,
  fallbackTitle,
  fallbackDescription,
  retry,
  compact,
}: ApiErrorStateProps) {
  const { t } = useTranslation(["errors", "common"]);
  const info = getApiErrorInfo(error, { fallbackTitle, fallbackDescription });

  const useFallback =
    info.kind === "unknown" && (Boolean(fallbackTitle) || Boolean(fallbackDescription));

  const duplicateFallback =
    Boolean(fallbackTitle) &&
    Boolean(fallbackDescription) &&
    fallbackTitle === fallbackDescription;

  const title = useFallback && fallbackTitle ? fallbackTitle : t(info.titleKey);
  const description =
    useFallback && fallbackDescription
      ? duplicateFallback
        ? t(info.descriptionKey)
        : fallbackDescription
      : t(info.descriptionKey);

  return (
    <Alert variant="destructive">
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription className={cn(compact ? "space-y-2" : "space-y-3")}>
        <p>{description}</p>
        <div
          className={cn(
            "rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-xs text-muted-foreground",
            compact ? "space-y-1" : "space-y-1.5",
          )}
        >
          <p className="font-medium text-foreground/90">
            {t("errors:api.errorCode")}: {info.code}
          </p>
          {info.traceId ? (
            <p className="break-all font-mono text-[11px]">
              {t("errors:api.traceId")}: {info.traceId}
            </p>
          ) : null}
        </div>
        {retry ? <div className={cn(!compact && "mt-1")}>{retry}</div> : null}
      </AlertDescription>
    </Alert>
  );
}
