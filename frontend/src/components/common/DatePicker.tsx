import { format, parseISO } from "date-fns";
import { enUS, tr } from "date-fns/locale";
import { CalendarIcon, X } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";

type DatePickerProps = {
  value: string | null;
  onChange: (value: string | null) => void;
  placeholder: string;
  clearLabel: string;
  locale: "tr" | "en";
  disabled?: boolean;
  id?: string;
  className?: string;
};

export function DatePicker({
  value,
  onChange,
  placeholder,
  clearLabel,
  locale,
  disabled = false,
  id,
  className,
}: DatePickerProps) {
  const dateLocale = locale === "tr" ? tr : enUS;
  const selectedDate = parseDateOnlyValue(value);
  const formattedValue = formatDateOnlyLabel(selectedDate, locale);

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          disabled={disabled}
          className={cn(
            "w-full justify-start text-left font-normal",
            !formattedValue && "text-muted-foreground",
            className,
          )}
        >
          <CalendarIcon className="mr-2 h-4 w-4" />
          {formattedValue ?? placeholder}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto p-2" align="start">
        <Calendar
          mode="single"
          selected={selectedDate}
          onSelect={(date) => onChange(toDateOnlyString(date))}
          locale={dateLocale}
          defaultMonth={selectedDate}
        />
        <div className="flex justify-end border-t border-border pt-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => onChange(null)}
            disabled={!value}
          >
            <X className="h-4 w-4" />
            {clearLabel}
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}

function parseDateOnlyValue(value: string | null): Date | undefined {
  if (!value?.trim()) {
    return undefined;
  }

  const parsed = parseISO(value.trim());
  if (Number.isNaN(parsed.getTime())) {
    return undefined;
  }

  return parsed;
}

function toDateOnlyString(date: Date | undefined): string | null {
  if (!date) {
    return null;
  }

  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function formatDateOnlyLabel(date: Date | undefined, locale: "tr" | "en"): string | null {
  if (!date) {
    return null;
  }

  const formatPattern = locale === "tr" ? "dd.MM.yyyy" : "MM/dd/yyyy";
  return format(date, formatPattern);
}
