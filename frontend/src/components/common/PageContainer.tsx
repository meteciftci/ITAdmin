import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

export type PageContainerVariant = "fluid" | "wide" | "form" | "reading";

const variantClasses: Record<PageContainerVariant, string> = {
  fluid: "max-w-none",
  wide: "mx-auto max-w-[1600px]",
  form: "mx-auto max-w-6xl",
  reading: "mx-auto max-w-3xl",
};

type PageContainerProps = {
  children: ReactNode;
  variant?: PageContainerVariant;
  className?: string;
};

/**
 * Owns page width and vertical rhythm. The application shell owns viewport
 * padding so pages do not accumulate nested horizontal gutters.
 */
export function PageContainer({
  children,
  variant = "wide",
  className,
}: PageContainerProps) {
  return (
    <section
      data-slot="page-container"
      className={cn(
        "flex w-full min-w-0 flex-col gap-6",
        variantClasses[variant],
        className,
      )}
    >
      {children}
    </section>
  );
}
