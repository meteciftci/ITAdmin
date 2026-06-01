import type { ReactNode } from "react";

import { parseJsonLikeValue } from "@/lib/parse-json-like-value";
import { cn } from "@/lib/utils";

type PrettyJsonBlockProps = {
  value: unknown;
  emptyLabel?: ReactNode;
  className?: string;
};

export function PrettyJsonBlock({ value, emptyLabel = "-", className }: PrettyJsonBlockProps) {
  const parsed = parseJsonLikeValue(value);

  if (parsed.kind === "empty") {
    return <span className="text-muted-foreground">{emptyLabel}</span>;
  }

  return (
    <pre
      className={cn(
        "max-h-64 overflow-auto rounded-md border bg-muted/30 p-3 font-mono text-xs whitespace-pre",
        className,
      )}
    >
      {parsed.text}
    </pre>
  );
}
