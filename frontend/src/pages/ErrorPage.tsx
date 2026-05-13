import { useCallback, useMemo, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import {
  AlertTriangle,
  ServerCrash,
  ShieldAlert,
  WifiOff,
} from "lucide-react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import type { ApiErrorKind } from "@/lib/api-error";
import {
  ERROR_ROUTE_SLUG_DEFAULTS,
  isRouteErrorState,
  resolveErrorRouteFromSlug,
  type RouteErrorState,
} from "@/lib/route-error";
import { cn } from "@/lib/utils";

type ErrorRouteIconProps = {
  kind?: ApiErrorKind;
  slug?: string;
};

function ErrorRouteIcon({ kind, slug }: ErrorRouteIconProps) {
  const iconClass = "size-8 shrink-0";
  if (slug === "not-found") {
    return <AlertTriangle className={iconClass} />;
  }
  switch (kind) {
    case "network":
      return <WifiOff className={iconClass} />;
    case "serviceUnavailable":
    case "server":
      return <ServerCrash className={iconClass} />;
    case "forbidden":
    case "unauthorized":
      return <ShieldAlert className={iconClass} />;
    case "validation":
    case "unknown":
    default:
      return <AlertTriangle className={iconClass} />;
  }
}

type DetailRowProps = {
  label: string;
  children: ReactNode;
  className?: string;
};

function DetailRow({ label, children, className }: DetailRowProps) {
  return (
    <div
      className={cn(
        "grid gap-1 border-b border-border/60 pb-3 last:border-b-0 last:pb-0 sm:grid-cols-[minmax(0,10.5rem)_1fr] sm:items-start sm:gap-x-6",
        className,
      )}
    >
      <dt className="text-xs font-medium text-muted-foreground sm:pt-0.5">{label}</dt>
      <dd className="min-w-0 text-sm text-foreground">{children}</dd>
    </div>
  );
}

export function ErrorPage() {
  const { t } = useTranslation(["errors", "common"]);
  const navigate = useNavigate();
  const location = useLocation();
  const params = useParams<{ code: string }>();

  const slug = params.code;
  const fromLocation = isRouteErrorState(location.state) ? location.state : undefined;
  const slugDefaults = resolveErrorRouteFromSlug(slug);

  const merged: Pick<
    RouteErrorState,
    | "code"
    | "kind"
    | "status"
    | "traceId"
    | "titleKey"
    | "descriptionKey"
    | "fromPath"
    | "retryPath"
    | "sourceLabel"
  > = useMemo(() => {
    if (fromLocation) {
      const base = slugDefaults ?? ERROR_ROUTE_SLUG_DEFAULTS["unknown-error"];
      return {
        code: fromLocation.code,
        kind: fromLocation.kind ?? base.kind,
        status: fromLocation.status,
        traceId: fromLocation.traceId ?? null,
        titleKey: fromLocation.titleKey ?? base.titleKey,
        descriptionKey: fromLocation.descriptionKey ?? base.descriptionKey,
        fromPath: fromLocation.fromPath,
        retryPath: fromLocation.retryPath,
        sourceLabel: fromLocation.sourceLabel,
      };
    }
    if (slugDefaults) {
      return {
        code: slugDefaults.code,
        kind: slugDefaults.kind,
        titleKey: slugDefaults.titleKey,
        descriptionKey: slugDefaults.descriptionKey,
        status: undefined,
        traceId: null,
        fromPath: undefined,
        retryPath: undefined,
        sourceLabel: undefined,
      };
    }
    return {
      code: "UNKNOWN_ERROR",
      kind: "unknown",
      titleKey: "errors:route.titleFallback",
      descriptionKey: "errors:route.descriptionFallback",
      status: undefined,
      traceId: null,
      fromPath: undefined,
      retryPath: undefined,
      sourceLabel: undefined,
    };
  }, [fromLocation, slugDefaults]);

  const title = t(merged.titleKey ?? "errors:route.titleFallback");
  const description = t(merged.descriptionKey ?? "errors:route.descriptionFallback");

  const sourceText = useMemo(() => {
    if (merged.sourceLabel && merged.fromPath) {
      return `${merged.sourceLabel} (${merged.fromPath})`;
    }
    if (merged.sourceLabel) return merged.sourceLabel;
    if (merged.fromPath) return merged.fromPath;
    return null;
  }, [merged.fromPath, merged.sourceLabel]);

  const handleRetry = useCallback(() => {
    if (merged.retryPath) {
      void navigate(merged.retryPath);
      return;
    }
    void navigate(-1);
  }, [merged.retryPath, navigate]);

  const handleBack = useCallback(() => {
    void navigate(-1);
  }, [navigate]);

  const handleDashboard = useCallback(() => {
    void navigate("/dashboard");
  }, [navigate]);

  const copyTraceId = useCallback(async () => {
    if (!merged.traceId) return;
    try {
      await navigator.clipboard.writeText(merged.traceId);
      toast.success(t("errors:route.traceIdCopied"));
    } catch {
      toast.error(t("errors:generic"));
    }
  }, [merged.traceId, t]);

  return (
    <div className="flex min-h-[min(72vh,40rem)] w-full items-center justify-center px-2 py-6 sm:px-4 sm:py-10">
      <div
        className={cn(
          "w-full max-w-3xl overflow-hidden rounded-2xl border bg-card text-card-foreground shadow-md",
        )}
      >
        <div className="relative border-b border-border/60 bg-gradient-to-br from-muted/50 via-muted/20 to-card px-6 pb-10 pt-8 sm:px-10 sm:pb-12 sm:pt-10">
          <div
            className="pointer-events-none absolute -right-16 -top-24 size-72 rounded-full bg-muted/35 blur-3xl sm:-right-20 sm:-top-28 sm:size-80"
            aria-hidden
          />
          <div className="relative flex flex-col items-center gap-8 sm:flex-row sm:items-start sm:gap-10">
            <div className="flex shrink-0 justify-center sm:pt-1">
              <div
                className="flex size-16 items-center justify-center rounded-2xl bg-destructive/10 text-destructive sm:size-[4.5rem]"
                aria-hidden
              >
                <ErrorRouteIcon kind={merged.kind} slug={slug} />
              </div>
            </div>
            <div className="min-w-0 flex-1 space-y-4 text-center sm:space-y-5 sm:pt-1 sm:text-left">
              <h1 className="text-balance text-2xl font-semibold tracking-tight text-foreground sm:text-[1.65rem]">
                {title}
              </h1>
              <p className="mx-auto max-w-prose text-pretty text-base leading-relaxed text-muted-foreground sm:mx-0">
                {description}
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-8 p-6 sm:p-10">
          <section
            className="rounded-lg border border-border bg-muted/30 p-4 sm:p-5"
            aria-labelledby="error-technical-heading"
          >
            <h2
              id="error-technical-heading"
              className="mb-4 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              {t("errors:route.technicalDetails")}
            </h2>
            <dl className="space-y-3">
              <DetailRow label={t("errors:route.errorCode")}>
                <code className="break-all font-mono text-sm font-medium">{merged.code}</code>
              </DetailRow>
              {merged.status !== undefined ? (
                <DetailRow label={t("errors:route.httpStatus")}>
                  <span className="font-mono text-sm">{merged.status}</span>
                </DetailRow>
              ) : null}
              {sourceText ? (
                <DetailRow label={t("errors:route.source")}>
                  <span className="break-words text-sm">{sourceText}</span>
                </DetailRow>
              ) : null}
              {merged.traceId ? (
                <DetailRow label={t("errors:route.traceId")} className="border-b-0 pb-0">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
                    <code className="break-all font-mono text-xs leading-relaxed text-foreground sm:text-sm">
                      {merged.traceId}
                    </code>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-8 w-full shrink-0 sm:w-auto sm:self-start"
                      onClick={() => void copyTraceId()}
                    >
                      {t("errors:route.copyTraceId")}
                    </Button>
                  </div>
                </DetailRow>
              ) : null}
            </dl>
          </section>

          <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:gap-3">
            <Button type="button" variant="default" className="w-full sm:w-auto" onClick={handleRetry}>
              {t("errors:route.retry")}
            </Button>
            <Button type="button" variant="outline" className="w-full sm:w-auto" onClick={handleBack}>
              {t("errors:route.back")}
            </Button>
            <Button
              type="button"
              variant="outline"
              className="w-full sm:w-auto"
              onClick={handleDashboard}
            >
              {t("errors:route.dashboard")}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
