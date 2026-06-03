import type { ReactNode } from "react";

export function AdUserDetailSectionTitle({ children }: { children: ReactNode }) {
  return <p className="text-xs font-medium text-muted-foreground">{children}</p>;
}

export function AdUserDetailField({
  label,
  value,
  children,
  valueClassName,
}: {
  label: string;
  value?: string | null;
  children?: ReactNode;
  valueClassName?: string;
}) {
  return (
    <div className="space-y-1">
      <p className="text-xs text-muted-foreground">{label}</p>
      {children ?? (
        <p className={valueClassName}>{value?.trim() ? value : "-"}</p>
      )}
    </div>
  );
}
