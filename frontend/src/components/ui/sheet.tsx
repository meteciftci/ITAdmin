import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

type SheetProps = {
  open: boolean;
  onOpenChange?: (open: boolean) => void;
  children: ReactNode;
};

type SheetContentProps = React.ComponentProps<"div"> & {
  side?: "left" | "right";
  onOpenChange?: (open: boolean) => void;
};

export function Sheet({ open, children }: SheetProps) {
  if (!open) return null;
  return <>{children}</>;
}

export function SheetContent({
  className,
  children,
  side = "right",
  onOpenChange,
  ...props
}: SheetContentProps) {
  return (
    <div
      className="fixed inset-0 z-50 bg-black/40"
      onClick={() => onOpenChange?.(false)}
      aria-hidden
    >
      <div
        role="dialog"
        aria-modal="true"
        className={cn(
          "absolute top-0 h-full w-full max-w-full overflow-y-auto border-border bg-card text-card-foreground shadow-xl md:max-w-[640px]",
          side === "right" ? "right-0 border-l" : "left-0 border-r",
          className,
        )}
        onClick={(event) => event.stopPropagation()}
        {...props}
      >
        {children}
      </div>
    </div>
  );
}

export function SheetHeader({ className, ...props }: React.ComponentProps<"div">) {
  return <div className={cn("space-y-1 border-b px-5 py-4", className)} {...props} />;
}

export function SheetTitle({ className, ...props }: React.ComponentProps<"h2">) {
  return <h2 className={cn("text-base font-semibold", className)} {...props} />;
}

export function SheetDescription({ className, ...props }: React.ComponentProps<"p">) {
  return <p className={cn("text-sm text-muted-foreground", className)} {...props} />;
}
