import type { ReactNode } from "react";

import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

type Props = {
  label: string;
  children?: ReactNode;
  value?: string | null;
  valueClassName?: string;
};

export function LicenseDetailField({ label, children, value, valueClassName }: Props) {
  return (
    <div className="space-y-1">
      <Label className="text-muted-foreground">{label}</Label>
      {children ?? (
        <p className={cn("text-sm", valueClassName)}>{value?.trim() ? value : "-"}</p>
      )}
    </div>
  );
}
