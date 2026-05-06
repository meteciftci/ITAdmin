import { useId } from "react";

import { Input } from "@/components/ui/input";

type DateRangeFilterProps = {
  from: string;
  to: string;
  onFromChange: (value: string) => void;
  onToChange: (value: string) => void;
  fromLabel: string;
  toLabel: string;
};

export function DateRangeFilter({
  from,
  to,
  onFromChange,
  onToChange,
  fromLabel,
  toLabel,
}: DateRangeFilterProps) {
  const fromInputId = useId();
  const toInputId = useId();

  const openDatePicker = (inputId: string) => {
    const input = document.getElementById(inputId) as HTMLInputElement | null;
    if (!input) return;
    input.focus();
    input.showPicker?.();
  };

  return (
    <div className="flex w-full min-w-0 flex-col gap-1 sm:w-auto">
      <div className="flex items-center gap-2 rounded-lg border bg-background p-2">
        <div
          role="button"
          tabIndex={0}
          className="w-full min-w-0 text-left sm:w-40"
          onClick={() => openDatePicker(fromInputId)}
          onKeyDown={(event) => {
            if (event.key === "Enter" || event.key === " ") {
              event.preventDefault();
              openDatePicker(fromInputId);
            }
          }}
        >
          <Input
            id={fromInputId}
            type="date"
            value={from}
            onChange={(event) => onFromChange(event.target.value)}
            aria-label={fromLabel}
          />
        </div>
        <span className="text-muted-foreground">-</span>
        <div
          role="button"
          tabIndex={0}
          className="w-full min-w-0 text-left sm:w-40"
          onClick={() => openDatePicker(toInputId)}
          onKeyDown={(event) => {
            if (event.key === "Enter" || event.key === " ") {
              event.preventDefault();
              openDatePicker(toInputId);
            }
          }}
        >
          <Input
            id={toInputId}
            type="date"
            value={to}
            onChange={(event) => onToChange(event.target.value)}
            aria-label={toLabel}
          />
        </div>
      </div>
    </div>
  );
}
