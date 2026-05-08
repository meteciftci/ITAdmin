import type { ComponentPropsWithoutRef, ReactNode } from "react";
import { ChevronRight } from "lucide-react";

import { cn } from "@/lib/utils";

type BreadcrumbProps = ComponentPropsWithoutRef<"nav">;

export function Breadcrumb({ className, ...props }: BreadcrumbProps) {
  return <nav aria-label="breadcrumb" className={cn("min-w-0", className)} {...props} />;
}

type BreadcrumbListProps = ComponentPropsWithoutRef<"ol">;

export function BreadcrumbList({ className, ...props }: BreadcrumbListProps) {
  return (
    <ol
      className={cn(
        "flex min-w-0 items-center gap-1 text-sm text-muted-foreground sm:gap-1.5",
        className,
      )}
      {...props}
    />
  );
}

type BreadcrumbItemProps = ComponentPropsWithoutRef<"li">;

export function BreadcrumbItem({ className, ...props }: BreadcrumbItemProps) {
  return <li className={cn("inline-flex min-w-0 items-center gap-1", className)} {...props} />;
}

type BreadcrumbLinkProps = ComponentPropsWithoutRef<"a"> & {
  children: ReactNode;
};

export function BreadcrumbLink({ className, children, ...props }: BreadcrumbLinkProps) {
  return (
    <a
      className={cn(
        "max-w-40 truncate rounded-sm transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
        "sm:max-w-56",
        className,
      )}
      {...props}
    >
      {children}
    </a>
  );
}

type BreadcrumbPageProps = ComponentPropsWithoutRef<"span">;

export function BreadcrumbPage({ className, ...props }: BreadcrumbPageProps) {
  return (
    <span
      aria-current="page"
      className={cn("max-w-40 truncate font-medium text-foreground sm:max-w-56", className)}
      {...props}
    />
  );
}

type BreadcrumbSeparatorProps = ComponentPropsWithoutRef<"li">;

export function BreadcrumbSeparator({ className, ...props }: BreadcrumbSeparatorProps) {
  return (
    <li
      aria-hidden="true"
      className={cn("inline-flex size-4 items-center justify-center text-muted-foreground/70", className)}
      {...props}
    >
      <ChevronRight className="size-3.5" />
    </li>
  );
}
