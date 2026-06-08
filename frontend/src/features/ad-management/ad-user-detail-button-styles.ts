import { buttonVariants } from "@/components/ui/button-variants";
import { cn } from "@/lib/utils";

const detailActionButtonBase = cn(
  buttonVariants({ size: "sm" }),
  "inline-flex h-8 min-h-8 items-center justify-center px-3 text-sm",
);

export const adDetailEditButtonClass = cn(
  detailActionButtonBase,
  "border border-amber-500/30 bg-amber-500/15 text-amber-700 hover:bg-amber-500/25",
  "dark:bg-amber-500/15 dark:text-amber-300 dark:hover:bg-amber-500/25",
);

/** @deprecated Use adDetailEditButtonClass */
export const adUserDetailEditButtonClass = adDetailEditButtonClass;

export const adUserDetailManagerChangeButtonClass = cn(
  detailActionButtonBase,
  "border border-sky-500/30 bg-sky-500/15 text-sky-800 hover:bg-sky-500/25",
  "dark:bg-sky-500/15 dark:text-sky-300 dark:hover:bg-sky-500/25",
);

export const adUserDetailManagerClearButtonClass = cn(
  detailActionButtonBase,
  "border border-destructive/30 bg-destructive/10 text-destructive hover:bg-destructive/20",
  "dark:bg-destructive/15 dark:text-red-300 dark:hover:bg-destructive/25",
);
