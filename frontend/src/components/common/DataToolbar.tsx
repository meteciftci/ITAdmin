import type { ReactNode } from "react";

import { Input } from "@/components/ui/input";

type DataToolbarProps = {
  searchValue?: string;
  onSearchChange?: (value: string) => void;
  searchPlaceholder?: string;
  children?: ReactNode;
  actions?: ReactNode;
};

export function DataToolbar({
  searchValue,
  onSearchChange,
  searchPlaceholder,
  children,
  actions,
}: DataToolbarProps) {
  return (
    <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
      <div className="flex min-w-0 flex-1 flex-col gap-3 sm:flex-row sm:items-center">
        {onSearchChange ? (
          <div className="min-w-[240px] flex-1">
            <Input
              value={searchValue ?? ""}
              onChange={(event) => onSearchChange(event.target.value)}
              placeholder={searchPlaceholder}
              className="w-full"
            />
          </div>
        ) : null}
        {children ? <div className="flex flex-wrap items-center gap-2">{children}</div> : null}
      </div>
      {actions ? (
        <div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
          {actions}
        </div>
      ) : null}
      </div>
  );
}
