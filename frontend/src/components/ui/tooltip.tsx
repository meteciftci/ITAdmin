import type { ReactNode } from "react";

type TooltipProps = {
  text: ReactNode;
  children: ReactNode;
  disabled?: boolean;
};

export function Tooltip({ text, children, disabled = false }: TooltipProps) {
  if (disabled) {
    return <>{children}</>;
  }

  return <span title={typeof text === "string" ? text : undefined}>{children}</span>;
}
