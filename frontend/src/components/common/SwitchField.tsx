import * as React from "react";

import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";

type SwitchFieldProps = {
  id: string;
  label: React.ReactNode;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
  description?: React.ReactNode;
  disabled?: boolean;
  className?: string;
};

export function SwitchField({
  id,
  label,
  checked,
  onCheckedChange,
  description,
  disabled,
  className,
}: SwitchFieldProps) {
  return (
    <div className={cn("flex items-start gap-3", className)}>
      <Switch
        id={id}
        checked={checked}
        onCheckedChange={onCheckedChange}
        disabled={disabled}
        className="mt-0.5"
      />
      <div className="min-w-0 space-y-0.5">
        <label
          htmlFor={id}
          className={cn(
            "block text-sm font-medium leading-snug select-none",
            disabled
              ? "cursor-not-allowed text-muted-foreground"
              : "cursor-pointer text-foreground",
          )}
        >
          {label}
        </label>
        {description ? (
          <p className="text-xs leading-snug text-muted-foreground">
            {description}
          </p>
        ) : null}
      </div>
    </div>
  );
}
