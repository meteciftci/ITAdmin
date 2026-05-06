import type { ReactNode } from "react";
import { cloneElement, isValidElement, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";

import { cn } from "@/lib/utils";

type DropdownMenuItemProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  children: ReactNode;
};

type DropdownMenuLabelProps = {
  children: ReactNode;
};

type DropdownMenuContentProps = {
  align?: "start" | "end";
  sideOffset?: number;
  collisionPadding?: number;
  avoidCollisions?: boolean;
};

export function DropdownMenuRoot({
  trigger,
  content,
  contentProps,
}: {
  trigger: ReactNode;
  content: ReactNode;
  contentProps?: DropdownMenuContentProps;
}) {
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLDivElement | null>(null);
  const contentRef = useRef<HTMLDivElement | null>(null);
  const [position, setPosition] = useState<{ top: number; left: number; origin: "top" | "bottom" }>({
    top: 0,
    left: 0,
    origin: "top",
  });

  const updatePosition = () => {
    if (!triggerRef.current || !contentRef.current) return;

    const triggerRect = triggerRef.current.getBoundingClientRect();
    const contentRect = contentRef.current.getBoundingClientRect();
    const viewportHeight = window.innerHeight;
    const viewportWidth = window.innerWidth;
    const sideOffset = contentProps?.sideOffset ?? 8;
    const collisionPadding = contentProps?.collisionPadding ?? 8;

    const openDownTop = triggerRect.bottom + sideOffset;
    const openUpTop = triggerRect.top - contentRect.height - sideOffset;
    const canOpenDown = openDownTop + contentRect.height <= viewportHeight - collisionPadding;

    const avoidCollisions = contentProps?.avoidCollisions ?? true;
    const top = avoidCollisions
      ? canOpenDown
        ? openDownTop
        : Math.max(collisionPadding, openUpTop)
      : openDownTop;
    const origin: "top" | "bottom" = canOpenDown ? "top" : "bottom";

    const align = contentProps?.align ?? "end";
    const preferredLeft =
      align === "start" ? triggerRect.left : triggerRect.right - contentRect.width;
    const left = avoidCollisions
      ? Math.min(
          Math.max(collisionPadding, preferredLeft),
          viewportWidth - contentRect.width - collisionPadding,
        )
      : preferredLeft;

    setPosition({ top, left, origin });
  };

  useEffect(() => {
    if (!open) return;

    updatePosition();
    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);

    return () => {
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
    };
  }, [open, contentProps?.align, contentProps?.collisionPadding, contentProps?.sideOffset]);

  const triggerNode = isValidElement(trigger)
    ? cloneElement(trigger, {
        onClick: () => setOpen((prev) => !prev),
      } as { onClick: () => void })
    : trigger;

  return (
    <div ref={triggerRef} className="relative inline-flex">
      {triggerNode}
      {open ? (
        <>
          <button
            type="button"
            className="fixed inset-0 z-40 cursor-default"
            onClick={() => setOpen(false)}
            aria-label="close"
          />
          {createPortal(
            <div
              ref={contentRef}
              className="fixed z-50 min-w-44 rounded-lg border bg-popover p-1 shadow-md"
              style={{
                top: position.top,
                left: position.left,
                transformOrigin: `right ${position.origin}`,
              }}
            >
              <div onClick={() => setOpen(false)}>{content}</div>
            </div>,
            document.body,
          )}
        </>
      ) : null}
    </div>
  );
}

export function DropdownMenuItem({ className, children, ...props }: DropdownMenuItemProps) {
  return (
    <button
      type="button"
      className={cn(
        "flex w-full items-center rounded-md px-2 py-1.5 text-left text-sm hover:bg-muted disabled:opacity-50",
        className,
      )}
      {...props}
    >
      {children}
    </button>
  );
}

export function DropdownMenuLabel({ children }: DropdownMenuLabelProps) {
  return <p className="px-2 py-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{children}</p>;
}

export function DropdownMenuSeparator() {
  return <div className="my-1 h-px bg-border" />;
}
