import type { ComponentProps } from "react";

import type { PopoverContent } from "@/components/ui/popover";

export const AD_COMBOBOX_TRIGGER_WRAPPER_CLASSNAME = "w-full [&>span]:flex [&>span]:w-full";

export const AD_COMBOBOX_TRIGGER_BUTTON_CLASSNAME =
  "flex h-10 w-full min-w-0 items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-left text-sm shadow-xs hover:bg-muted/30 disabled:cursor-not-allowed disabled:opacity-50";

export const AD_COMBOBOX_TRIGGER_LABEL_CLASSNAME = "min-w-0 flex-1 truncate";

export const AD_COMBOBOX_POPOVER_CONTENT_PROPS = {
  matchTriggerWidth: true,
  className: "p-2",
  align: "start",
} as const satisfies Pick<ComponentProps<typeof PopoverContent>, "matchTriggerWidth" | "className" | "align">;
