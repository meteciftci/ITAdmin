import type { ReactNode } from "react";
import { AlertTriangle, Info } from "lucide-react";

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { cn } from "@/lib/utils";

type BlockingStateCardVariant = "warning" | "danger" | "info";
type BlockingStateCardSize = "default" | "compact";
type BlockingStateCardWidth = "contained" | "full";

type BlockingStateCardProps = {
  icon?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  details?: ReactNode;
  actions?: ReactNode;
  variant?: BlockingStateCardVariant;
  size?: BlockingStateCardSize;
  width?: BlockingStateCardWidth;
  centered?: boolean;
  className?: string;
};

const variantStyles: Record<
  BlockingStateCardVariant,
  { ring: string; iconBg: string; defaultIcon: typeof AlertTriangle }
> = {
  warning: {
    ring: "ring-2 ring-amber-500/15",
    iconBg: "bg-amber-500/10 text-amber-600 dark:text-amber-400",
    defaultIcon: AlertTriangle,
  },
  danger: {
    ring: "ring-2 ring-destructive/15",
    iconBg: "bg-destructive/10 text-destructive",
    defaultIcon: AlertTriangle,
  },
  info: {
    ring: "ring-2 ring-primary/10",
    iconBg: "bg-primary/10 text-primary",
    defaultIcon: Info,
  },
};

export function BlockingStateCard({
  icon,
  title,
  description,
  details,
  actions,
  variant = "warning",
  size = "default",
  width = "contained",
  centered = false,
  className,
}: BlockingStateCardProps) {
  const styles = variantStyles[variant];
  const DefaultIcon = styles.defaultIcon;
  const resolvedIcon = icon ?? (
    <DefaultIcon className="size-5 shrink-0" aria-hidden />
  );

  const isFullWidth = width === "full";

  return (
    <div
      role="alert"
      aria-live={variant === "danger" ? "assertive" : "polite"}
      className={cn(
        "flex w-full",
        !isFullWidth && "justify-center",
        centered
          && (size === "compact"
            ? "min-h-[140px] py-6"
            : "min-h-[min(560px,calc(100dvh-10rem))] items-center py-10"),
        !centered && "py-1",
        className,
      )}
    >
      <Card
        className={cn(
          "border-border/80 bg-card shadow-md",
          styles.ring,
          isFullWidth
            ? "w-full max-w-none"
            : size === "compact"
              ? "w-full max-w-lg"
              : "w-full max-w-2xl",
        )}
      >
        <CardHeader className={cn("space-y-2", size === "compact" && "pb-3")}>
          <div className="flex items-start gap-3">
            <div
              className={cn(
                "mt-0.5 shrink-0 rounded-md p-2",
                styles.iconBg,
              )}
            >
              {resolvedIcon}
            </div>
            <div
              className={cn(
                "min-w-0 space-y-1",
                isFullWidth && "max-w-4xl",
              )}
            >
              <CardTitle
                className={cn(
                  "text-pretty leading-tight",
                  size === "compact" ? "text-base md:text-lg" : "text-lg md:text-xl",
                )}
              >
                {title}
              </CardTitle>
              {description ? (
                <CardDescription
                  className={cn(
                    "text-pretty",
                    size === "compact" ? "text-sm" : "text-sm md:text-base",
                  )}
                >
                  {description}
                </CardDescription>
              ) : null}
            </div>
          </div>
        </CardHeader>

        {details || actions ? (
          <CardContent
            className={cn(
              "space-y-4",
              size === "compact" && "pt-0",
            )}
          >
            {details ? (
              <div
                className={cn(
                  "space-y-2 text-sm text-muted-foreground md:text-[15px]",
                  isFullWidth && "max-w-4xl",
                )}
              >
                {details}
              </div>
            ) : null}
            {actions ? (
              <div className="flex flex-wrap gap-2">{actions}</div>
            ) : null}
          </CardContent>
        ) : null}
      </Card>
    </div>
  );
}
