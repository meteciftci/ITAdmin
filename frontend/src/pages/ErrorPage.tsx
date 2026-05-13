import { useCallback, useMemo } from "react";
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
  if (slug === "not-found") {
    return <AlertTriangle className="size-6" />;
  }
  switch (kind) {
    case "network":
      return <WifiOff className="size-6" />;
    case "serviceUnavailable":
    case "server":
      return <ServerCrash className="size-6" />;
    case "forbidden":
    case "unauthorized":
      return <ShieldAlert className="size-6" />;
    case "validation":
    case "unknown":
    default:
      return <AlertTriangle className="size-6" />;
  }
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
    <div className="flex min-h-[min(70vh,36rem)] items-center justify-center">
      <div
        className={cn(
          "w-full max-w-lg rounded-xl border bg-card p-6 shadow-sm",
          "text-card-foreground",
        )}
      >
        <div className="flex flex-col items-center text-center sm:flex-row sm:items-start sm:gap-4 sm:text-left">
          <div
            className="mb-4 flex size-12 shrink-0 items-center justify-center rounded-full bg-muted text-muted-foreground sm:mb-0"
            aria-hidden
          >
            <ErrorRouteIcon kind={merged.kind} slug={slug} />
          </div>
          <div className="min-w-0 flex-1 space-y-3">
            <div>
              <h1 className="text-xl font-semibold tracking-tight text-foreground">
                {title}
              </h1>
              <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
                {description}
              </p>
            </div>

            <div className="flex flex-wrap items-center justify-center gap-2 sm:justify-start">
              <span className="inline-flex items-center rounded-md border border-border bg-muted/50 px-2.5 py-1 font-mono text-xs font-medium text-foreground">
                {t("errors:route.errorCode")}: {merged.code}
              </span>
              {merged.status !== undefined ? (
                <span className="inline-flex items-center rounded-md border border-border bg-muted/50 px-2.5 py-1 text-xs text-muted-foreground">
                  {t("errors:route.httpStatus")}: {merged.status}
                </span>
              ) : null}
            </div>

            {merged.traceId ? (
              <div className="rounded-lg border border-border bg-muted/30 p-3 text-left">
                <div className="mb-1.5 text-xs font-medium text-muted-foreground">
                  {t("errors:route.traceId")}
                </div>
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <code className="break-all text-xs text-foreground">{merged.traceId}</code>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="shrink-0 self-start sm:self-center"
                    onClick={() => void copyTraceId()}
                  >
                    {t("errors:route.copyTraceId")}
                  </Button>
                </div>
              </div>
            ) : null}

            {merged.sourceLabel || merged.fromPath ? (
              <p className="text-xs text-muted-foreground">
                {merged.sourceLabel ? (
                  <>
                    <span className="font-medium text-foreground/80">
                      {t("errors:route.source")}:
                    </span>{" "}
                    {merged.sourceLabel}
                    {merged.fromPath ? ` (${merged.fromPath})` : null}
                  </>
                ) : merged.fromPath ? (
                  <>
                    <span className="font-medium text-foreground/80">
                      {t("errors:route.source")}:
                    </span>{" "}
                    {merged.fromPath}
                  </>
                ) : null}
              </p>
            ) : null}

            <div className="flex flex-wrap justify-center gap-2 pt-2 sm:justify-start">
              <Button type="button" variant="default" onClick={handleRetry}>
                {t("errors:route.retry")}
              </Button>
              <Button type="button" variant="outline" onClick={handleBack}>
                {t("errors:route.back")}
              </Button>
              <Button type="button" variant="outline" onClick={handleDashboard}>
                {t("errors:route.dashboard")}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
