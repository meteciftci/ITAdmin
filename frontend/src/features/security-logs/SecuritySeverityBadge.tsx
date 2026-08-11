import type { TFunction } from "i18next";
import { CircleAlert, CircleCheck, Info, TriangleAlert } from "lucide-react";

import { Badge } from "@/components/ui/badge";

function getSeverityBadgeVariant(
  severity: string,
): "default" | "secondary" | "outline" | "info" | "success" | "warning" | "destructive" {
  const normalizedSeverity = severity.trim().toLocaleLowerCase();

  if (normalizedSeverity === "info") return "info";
  if (normalizedSeverity === "low") return "secondary";
  if (normalizedSeverity === "warning") return "warning";
  if (["error", "critical", "high"].includes(normalizedSeverity)) return "destructive";
  if (normalizedSeverity === "success") return "success";
  return "secondary";
}

export function SecuritySeverityBadge({
  severity,
  t,
}: {
  severity: string;
  t: TFunction;
}) {
  const normalizedSeverity = severity.trim().toLocaleLowerCase();
  const label = t(`securityLogs:severities.${normalizedSeverity}`, {
    defaultValue: severity,
  });
  const iconClassName = "size-3.5 shrink-0";
  const icon =
    normalizedSeverity === "warning" ? (
      <TriangleAlert className={iconClassName} aria-hidden />
    ) : ["error", "critical", "high"].includes(normalizedSeverity) ? (
      <CircleAlert className={iconClassName} aria-hidden />
    ) : normalizedSeverity === "success" ? (
      <CircleCheck className={iconClassName} aria-hidden />
    ) : (
      <Info className={iconClassName} aria-hidden />
    );

  return (
    <Badge variant={getSeverityBadgeVariant(severity)} className="gap-1.5 whitespace-nowrap">
      {icon}
      {label}
    </Badge>
  );
}
