import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

type CodeBadgeProps = {
  children: ReactNode;
  className?: string;
};

export function CodeBadge({ children, className }: CodeBadgeProps) {
  return (
    <code
      className={cn(
        "inline-flex items-center rounded-md border border-border bg-muted px-1.5 py-0.5 font-mono text-xs text-foreground",
        className,
      )}
    >
      {children}
    </code>
  );
}
