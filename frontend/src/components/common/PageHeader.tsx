import type { ReactNode } from "react";

type PageHeaderProps = {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
};

export function PageHeader({ title, description, actions }: PageHeaderProps) {
  return (
    <header
      data-slot="page-header"
      className="flex w-full min-w-0 flex-col gap-x-6 gap-y-4 lg:flex-row lg:items-start lg:justify-between"
    >
      <div className="min-w-0 flex-1 space-y-1.5">
        <h1 className="text-2xl font-semibold tracking-[-0.025em] sm:text-[1.75rem]">
          {title}
        </h1>
        {description ? (
          <p className="max-w-2xl text-sm leading-6 text-muted-foreground">{description}</p>
        ) : null}
      </div>
      {actions ? (
        <div className="flex shrink-0 flex-wrap items-center gap-2 lg:justify-end">
          {actions}
        </div>
      ) : null}
    </header>
  );
}
