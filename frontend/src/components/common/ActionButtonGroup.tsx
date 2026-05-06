import type { ReactNode } from "react";

type ActionButtonGroupProps = {
  children: ReactNode;
};

export function ActionButtonGroup({ children }: ActionButtonGroupProps) {
  return <div className="flex flex-wrap items-center gap-1">{children}</div>;
}
