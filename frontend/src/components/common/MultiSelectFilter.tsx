import { useMemo, useState } from "react";
import { Check, ChevronDown, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";

type MultiSelectFilterProps = {
  label?: string;
  placeholder?: string;
  options: string[];
  selectedValues: string[];
  onChange: (values: string[]) => void;
  clearLabel: string;
  emptyLabel: string;
  searchPlaceholder: string;
};

export function MultiSelectFilter({
  label,
  placeholder,
  options,
  selectedValues,
  onChange,
  clearLabel,
  emptyLabel,
  searchPlaceholder,
}: MultiSelectFilterProps) {
  const [searchTerm, setSearchTerm] = useState("");

  const normalizedSelectedValues = useMemo(
    () => selectedValues.filter((value) => !isNullOrWhiteSpace(value)),
    [selectedValues],
  );

  const filteredOptions = useMemo(() => {
    const normalizedSearchTerm = searchTerm.trim().toLocaleLowerCase();
    if (!normalizedSearchTerm) {
      return options;
    }

    return options.filter((option) =>
      option.toLocaleLowerCase().includes(normalizedSearchTerm),
    );
  }, [options, searchTerm]);

  const placeholderText = placeholder ?? label ?? "";

  const handleToggle = (value: string) => {
    if (normalizedSelectedValues.includes(value)) {
      onChange(normalizedSelectedValues.filter((item) => item !== value));
      return;
    }

    onChange([...normalizedSelectedValues, value]);
  };

  const handleRemove = (value: string) => {
    onChange(normalizedSelectedValues.filter((item) => item !== value));
  };

  const handleClearAll = () => {
    if (!normalizedSelectedValues.length) {
      return;
    }

    onChange([]);
  };

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          className={cn(
            "h-auto min-h-10 w-full justify-between gap-2 py-2 text-left sm:w-[280px]",
            !normalizedSelectedValues.length && "text-muted-foreground",
          )}
        >
          <span className="flex min-w-0 flex-1 flex-wrap items-center gap-1.5">
            {normalizedSelectedValues.length ? (
              normalizedSelectedValues.map((value) => (
                <Badge
                  key={value}
                  variant="secondary"
                  className="max-w-full gap-1 py-0.5"
                >
                  <span className="truncate">{value}</span>
                  <button
                    type="button"
                    className="rounded-sm p-0.5 hover:bg-muted"
                    onClick={(event) => {
                      event.preventDefault();
                      event.stopPropagation();
                      handleRemove(value);
                    }}
                    aria-label={value}
                  >
                    <X className="h-3 w-3" />
                  </button>
                </Badge>
              ))
            ) : (
              <span>{placeholderText}</span>
            )}
          </span>
          <ChevronDown className="h-4 w-4 shrink-0 opacity-70" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[320px] space-y-2 p-2" align="start">
        <Input
          value={searchTerm}
          onChange={(event) => setSearchTerm(event.target.value)}
          placeholder={searchPlaceholder}
        />
        <div className="max-h-64 space-y-1 overflow-y-auto pr-1">
          {filteredOptions.length ? (
            filteredOptions.map((option) => {
              const isSelected = normalizedSelectedValues.includes(option);

              return (
                <button
                  key={option}
                  type="button"
                  className="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm hover:bg-muted"
                  onClick={() => handleToggle(option)}
                >
                  <Checkbox checked={isSelected} readOnly />
                  <span className="flex-1 truncate">{option}</span>
                  {isSelected ? <Check className="h-4 w-4 text-primary" /> : null}
                </button>
              );
            })
          ) : (
            <p className="px-2 py-3 text-sm text-muted-foreground">{emptyLabel}</p>
          )}
        </div>
        <div className="flex justify-end border-t border-border pt-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={handleClearAll}
            disabled={!normalizedSelectedValues.length}
          >
            <X className="h-4 w-4" />
            {clearLabel}
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}

function isNullOrWhiteSpace(value: string): boolean {
  return value.trim().length === 0;
}
