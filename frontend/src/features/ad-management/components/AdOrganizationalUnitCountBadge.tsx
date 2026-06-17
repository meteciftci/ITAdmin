import type { LucideIcon } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";
import { formatAdOrganizationalUnitCount } from "@/features/ad-management/ad-ou-display-labels";
import { cn } from "@/lib/utils";

type AdOrganizationalUnitCountBadgeProps = {
  label: string;
  value: number | null | undefined;
  icon: LucideIcon;
  variant?: "badge" | "card";
};

export function AdOrganizationalUnitCountBadge({
  label,
  value,
  icon: Icon,
  variant = "badge",
}: AdOrganizationalUnitCountBadgeProps) {
  const { t } = useTranslation(["common"]);
  const displayValue = formatAdOrganizationalUnitCount(value, t("common:notAvailable"));

  if (variant === "card") {
    return (
      <div
        className="flex min-w-0 items-start gap-3 rounded-lg border bg-card p-3"
        aria-label={label}
      >
        <div className="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted">
          <Icon className="size-4 text-muted-foreground" aria-hidden />
        </div>
        <div className="min-w-0 space-y-0.5">
          <p className="text-xs text-muted-foreground">{label}</p>
          <p className="text-lg font-semibold tabular-nums">{displayValue}</p>
        </div>
      </div>
    );
  }

  return (
    <Badge
      variant="outline"
      className={cn("inline-flex max-w-full gap-1.5 px-2 py-1 font-normal")}
      title={label}
      aria-label={`${label}: ${displayValue}`}
    >
      <Icon className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
      <span className="tabular-nums">{displayValue}</span>
    </Badge>
  );
}
