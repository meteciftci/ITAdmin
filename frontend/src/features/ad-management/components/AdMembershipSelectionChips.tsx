import { X } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export type AdMembershipSelectionChipItem = {
  key: string;
  primaryLabel: string;
  secondaryLabel?: string | null;
  distinguishedName?: string;
};

type Props = {
  title: string;
  emptyMessage?: string;
  items: AdMembershipSelectionChipItem[];
  onRemove: (key: string) => void;
  disabled?: boolean;
  removeAriaLabel: string;
};

export function AdMembershipSelectionChips({
  title,
  emptyMessage,
  items,
  onRemove,
  disabled,
  removeAriaLabel,
}: Props) {
  return (
    <div className="space-y-2">
      <p className="text-sm font-medium">{title}</p>
      {items.length === 0 ? (
        emptyMessage ? (
          <p className="text-sm text-muted-foreground">{emptyMessage}</p>
        ) : null
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((item) => (
            <li
              key={item.key}
              className="flex items-start gap-2 rounded-md border bg-muted/20 px-3 py-2"
            >
              <div className="min-w-0 flex-1">
                <p className="truncate font-medium">{item.primaryLabel}</p>
                {item.secondaryLabel ? (
                  <p
                    className="truncate text-xs text-muted-foreground"
                    title={item.secondaryLabel}
                  >
                    {item.secondaryLabel}
                  </p>
                ) : null}
                {item.distinguishedName ? (
                  <p
                    className="mt-1 truncate font-mono text-xs text-muted-foreground"
                    title={item.distinguishedName}
                  >
                    {item.distinguishedName}
                  </p>
                ) : null}
              </div>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className={cn("size-8 shrink-0")}
                onClick={() => onRemove(item.key)}
                disabled={disabled}
                aria-label={removeAriaLabel}
              >
                <X className="size-4" />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
