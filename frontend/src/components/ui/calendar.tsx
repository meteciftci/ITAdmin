import type { ComponentProps } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { DayPicker } from "react-day-picker";

import { buttonVariants } from "@/components/ui/button-variants";
import { cn } from "@/lib/utils";

type CalendarProps = ComponentProps<typeof DayPicker>;

function usesDropdownCaption(captionLayout: CalendarProps["captionLayout"]): boolean {
  return typeof captionLayout === "string" && captionLayout.startsWith("dropdown");
}

export function Calendar({
  className,
  classNames,
  showOutsideDays = true,
  captionLayout,
  ...props
}: CalendarProps) {
  const dropdownCaption = usesDropdownCaption(captionLayout);

  return (
    <DayPicker
      showOutsideDays={showOutsideDays}
      captionLayout={captionLayout}
      className={cn("p-2", className)}
      classNames={{
        months: "flex flex-col gap-4 sm:flex-row sm:gap-6",
        month: dropdownCaption
          ? "grid grid-cols-[auto_minmax(0,1fr)_auto] grid-rows-[auto_auto] items-center gap-x-1 gap-y-4"
          : "relative space-y-4",
        caption: dropdownCaption
          ? "flex items-center justify-center gap-2 pt-1"
          : "relative flex items-center justify-center gap-2 pt-1",
        caption_label: dropdownCaption ? "sr-only" : "text-sm font-medium",
        dropdowns: "flex items-center justify-center gap-2",
        dropdown_root: "relative inline-flex items-center",
        dropdown: cn(
          "h-8 rounded-md border border-input bg-background px-2 text-sm shadow-xs",
          "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
        ),
        months_dropdown: "h-8",
        years_dropdown: "h-8",
        nav: "flex items-center gap-1",
        button_previous: cn(
          buttonVariants({ variant: "outline", size: "icon-sm" }),
          dropdownCaption
            ? "col-start-1 row-start-1 h-7 w-7 shrink-0 border-border bg-popover p-0 text-popover-foreground"
            : "absolute left-1 h-7 w-7 border-border bg-popover p-0 text-popover-foreground",
        ),
        button_next: cn(
          buttonVariants({ variant: "outline", size: "icon-sm" }),
          dropdownCaption
            ? "col-start-3 row-start-1 h-7 w-7 shrink-0 border-border bg-popover p-0 text-popover-foreground"
            : "absolute right-1 h-7 w-7 border-border bg-popover p-0 text-popover-foreground",
        ),
        month_caption: dropdownCaption
          ? "col-start-2 row-start-1 flex min-w-0 items-center justify-center gap-2"
          : "flex items-center justify-center",
        month_grid: dropdownCaption ? "col-span-3 row-start-2" : undefined,
        weekdays: "flex",
        weekday: "w-9 text-xs font-normal text-muted-foreground",
        week: "mt-2 flex w-full",
        day: cn(
          buttonVariants({ variant: "ghost", size: "icon-sm" }),
          "h-9 w-9 p-0 font-normal aria-selected:opacity-100",
        ),
        day_button: "h-9 w-9 p-0 font-normal",
        range_start: "rounded-l-md bg-primary text-primary-foreground",
        range_end: "rounded-r-md bg-primary text-primary-foreground",
        range_middle: "rounded-none bg-accent text-accent-foreground",
        today: "border border-border",
        selected: "bg-primary text-primary-foreground hover:bg-primary hover:text-primary-foreground",
        outside: "text-muted-foreground opacity-45",
        disabled: "text-muted-foreground opacity-40",
        hidden: "invisible",
        ...classNames,
      }}
      components={{
        Chevron: ({ orientation, ...iconProps }) =>
          orientation === "left" ? <ChevronLeft className="h-4 w-4" {...iconProps} /> : <ChevronRight className="h-4 w-4" {...iconProps} />,
      }}
      {...props}
    />
  );
}
