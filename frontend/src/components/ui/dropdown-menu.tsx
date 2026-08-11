import type { ReactNode } from "react";
import { cloneElement, isValidElement, useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";

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
  const { t } = useTranslation("common");
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLDivElement | null>(null);
  const contentRef = useRef<HTMLDivElement | null>(null);
  const [position, setPosition] = useState<{ top: number; left: number; origin: "top" | "bottom" }>({
    top: 0,
    left: 0,
    origin: "top",
  });

  const focusTrigger = useCallback(() => {
    triggerRef.current?.querySelector<HTMLElement>("button, [href], [tabindex]")?.focus();
  }, []);

  const closeAndRestoreFocus = useCallback(() => {
    setOpen(false);
    window.setTimeout(focusTrigger, 0);
  }, [focusTrigger]);

  const updatePosition = useCallback(() => {
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
  }, [
    contentProps?.align,
    contentProps?.avoidCollisions,
    contentProps?.collisionPadding,
    contentProps?.sideOffset,
  ]);

  useEffect(() => {
    if (!open) return;

    let raf2 = 0;
    const raf1 = requestAnimationFrame(() => {
      updatePosition();
      raf2 = requestAnimationFrame(updatePosition);
    });

    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);

    const focusableItems = () =>
      Array.from(
        contentRef.current?.querySelectorAll<HTMLElement>(
          '[role="menuitem"]:not(:disabled)',
        ) ?? [],
      );
    const focusRaf = requestAnimationFrame(() => focusableItems()[0]?.focus());
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        closeAndRestoreFocus();
        return;
      }

      if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
      const items = focusableItems();
      if (!items.length) return;
      event.preventDefault();
      const currentIndex = items.indexOf(document.activeElement as HTMLElement);
      const delta = event.key === "ArrowDown" ? 1 : -1;
      const nextIndex = (currentIndex + delta + items.length) % items.length;
      items[nextIndex]?.focus();
    };
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      cancelAnimationFrame(raf1);
      cancelAnimationFrame(raf2);
      cancelAnimationFrame(focusRaf);
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [closeAndRestoreFocus, open, updatePosition]);

  const triggerNode = isValidElement(trigger)
    ? cloneElement(trigger, {
        onClick: () => setOpen((prev) => !prev),
        "aria-haspopup": "menu",
        "aria-expanded": open,
      } as { onClick: () => void; "aria-haspopup": "menu"; "aria-expanded": boolean })
    : trigger;

  return (
    <div ref={triggerRef} className="relative inline-flex">
      {triggerNode}
      {open ? (
        <>
          <button
            type="button"
            className="fixed inset-0 z-40 cursor-default"
            onClick={closeAndRestoreFocus}
            aria-label={t("actions.close")}
          />
          {createPortal(
            <div
              ref={contentRef}
              role="menu"
              className="fixed z-50 min-w-48 rounded-xl border bg-popover p-1.5 shadow-xl"
              style={{
                top: position.top,
                left: position.left,
                transformOrigin: `right ${position.origin}`,
              }}
            >
              <div
                onClick={() => {
                  setOpen(false);
                  focusTrigger();
                }}
              >
                {content}
              </div>
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
      role="menuitem"
      className={cn(
        "flex min-h-9 w-full items-center rounded-lg px-2.5 py-2 text-left text-sm hover:bg-muted disabled:opacity-50",
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
