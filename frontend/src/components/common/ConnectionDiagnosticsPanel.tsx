import { AlertCircle, CheckCircle2, Clock3, TriangleAlert } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

export type ConnectionDiagnosticDetail = {
  key: string;
  status: string;
  messageKey: string;
  messageParams?: Record<string, string | number | boolean> | null;
};

type Props = {
  title: string;
  description?: string;
  isValid: boolean;
  checkedAt?: string | null;
  details: ConnectionDiagnosticDetail[];
  resolveMessage: (detail: ConnectionDiagnosticDetail) => string;
  successLabel: string;
  failureLabel: string;
  warningLabel: string;
  checkedAtLabel?: string;
};

export function ConnectionDiagnosticsPanel({
  title,
  description,
  isValid,
  checkedAt,
  details,
  resolveMessage,
  successLabel,
  failureLabel,
  warningLabel,
  checkedAtLabel,
}: Props) {
  return (
    <section className="space-y-4 rounded-xl border bg-card p-4 shadow-sm" aria-live="polite">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold">{title}</h3>
          {description ? <p className="mt-1 text-sm text-muted-foreground">{description}</p> : null}
        </div>
        <Badge variant={isValid ? "success" : "destructive"}>
          {isValid ? successLabel : failureLabel}
        </Badge>
      </div>

      <div className="divide-y rounded-lg border">
        {details.map((detail, index) => {
          const isOk = detail.status === "Ok";
          const isWarning = detail.status === "Warning";
          const Icon = isOk ? CheckCircle2 : isWarning ? TriangleAlert : AlertCircle;
          return (
            <div key={`${detail.key}-${index}`} className="flex items-start gap-3 px-3 py-3">
              <Icon
                className={cn(
                  "mt-0.5 size-4 shrink-0",
                  isOk ? "text-emerald-600 dark:text-emerald-400" : isWarning ? "text-amber-600 dark:text-amber-400" : "text-destructive",
                )}
                aria-hidden="true"
              />
              <div className="min-w-0 flex-1">
                <p className="text-sm">{resolveMessage(detail)}</p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {isOk ? successLabel : isWarning ? warningLabel : failureLabel}
                </p>
              </div>
            </div>
          );
        })}
      </div>

      {checkedAt && checkedAtLabel ? (
        <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <Clock3 className="size-3.5" aria-hidden="true" />
          {checkedAtLabel}: {new Date(checkedAt).toLocaleString()}
        </p>
      ) : null}
    </section>
  );
}
