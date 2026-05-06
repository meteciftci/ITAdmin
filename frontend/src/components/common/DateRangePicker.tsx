import { format } from "date-fns";
import { enUS, tr } from "date-fns/locale";
import { CalendarIcon, X } from "lucide-react";
import type { DateRange } from "react-day-picker";

import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";

type DateRangePickerProps = {
  value: DateRange | undefined;
  onChange: (value: DateRange | undefined) => void;
  placeholder: string;
  clearLabel: string;
  locale: "tr" | "en";
};

export function DateRangePicker({
  value,
  onChange,
  placeholder,
  clearLabel,
  locale,
}: DateRangePickerProps) {
  const dateLocale = locale === "tr" ? tr : enUS;
  const formattedValue = getRangeLabel(value, locale);

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          className={cn(
            "w-full justify-start text-left font-normal sm:w-[300px]",
            !formattedValue && "text-muted-foreground",
          )}
        >
          <CalendarIcon className="mr-2 h-4 w-4" />
          {formattedValue ?? placeholder}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto p-2" align="start">
        <Calendar
          mode="range"
          numberOfMonths={2}
          selected={value}
          onSelect={onChange}
          locale={dateLocale}
          defaultMonth={value?.from}
        />
        <div className="flex justify-end border-t border-border pt-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => onChange(undefined)}
            disabled={!value?.from && !value?.to}
          >
            <X className="h-4 w-4" />
            {clearLabel}
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}

function getRangeLabel(value: DateRange | undefined, locale: "tr" | "en"): string | null {
  if (!value?.from) return null;

  const formatPattern = locale === "tr" ? "dd.MM.yyyy" : "MM/dd/yyyy";
  const fromLabel = format(value.from, formatPattern);

  if (!value.to) {
    return fromLabel;
  }

  const toLabel = format(value.to, formatPattern);
  return `${fromLabel} - ${toLabel}`;
}
