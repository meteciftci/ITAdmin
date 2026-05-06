import type { HTMLAttributes, ReactElement, ReactNode } from "react";
import {
  cloneElement,
  createContext,
  isValidElement,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { createPortal } from "react-dom";

import { cn } from "@/lib/utils";

type PopoverContextValue = {
  open: boolean;
  setOpen: (open: boolean) => void;
  triggerRef: React.RefObject<HTMLElement | null>;
  contentRef: React.RefObject<HTMLDivElement | null>;
};

const PopoverContext = createContext<PopoverContextValue | null>(null);

function usePopoverContext() {
  const context = useContext(PopoverContext);
  if (!context) {
    throw new Error("Popover components must be used within Popover.");
  }
  return context;
}

type PopoverProps = {
  children: ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
};

export function Popover({ children, open, onOpenChange }: PopoverProps) {
  const [internalOpen, setInternalOpen] = useState(false);
  const triggerRef = useRef<HTMLElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);

  const isControlled = typeof open === "boolean";
  const currentOpen = isControlled ? open : internalOpen;

  const setOpen = useCallback(
    (nextOpen: boolean) => {
      if (!isControlled) {
        setInternalOpen(nextOpen);
      }
      onOpenChange?.(nextOpen);
    },
    [isControlled, onOpenChange],
  );

  const contextValue = useMemo(
    () => ({
      open: currentOpen,
      setOpen,
      triggerRef,
      contentRef,
    }),
    [currentOpen, setOpen],
  );

  return <PopoverContext.Provider value={contextValue}>{children}</PopoverContext.Provider>;
}

type PopoverTriggerProps = {
  asChild?: boolean;
  children: ReactNode;
};

export function PopoverTrigger({ asChild, children }: PopoverTriggerProps) {
  const { open, setOpen, triggerRef } = usePopoverContext();
  const onClick = () => setOpen(!open);

  if (asChild && isValidElement(children)) {
    const child = children as ReactElement<{ onClick?: () => void }>;
    return (
      <span ref={triggerRef} className="inline-flex">
        {cloneElement(child, {
          onClick: () => {
            child.props.onClick?.();
            onClick();
          },
        })}
      </span>
    );
  }

  return (
    <button
      ref={(node) => {
        triggerRef.current = node;
      }}
      type="button"
      onClick={onClick}
    >
      {children}
    </button>
  );
}

type PopoverContentProps = HTMLAttributes<HTMLDivElement> & {
  align?: "start" | "end";
  sideOffset?: number;
};

export function PopoverContent({
  className,
  children,
  align = "start",
  sideOffset = 8,
  ...props
}: PopoverContentProps) {
  const { open, setOpen, triggerRef, contentRef } = usePopoverContext();
  const [position, setPosition] = useState({ top: 0, left: 0 });

  const updatePosition = useCallback(() => {
    if (!triggerRef.current || !contentRef.current) return;

    const triggerRect = triggerRef.current.getBoundingClientRect();
    const contentRect = contentRef.current.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const padding = 8;

    const preferredTop = triggerRect.bottom + sideOffset;
    const fallbackTop = triggerRect.top - contentRect.height - sideOffset;
    const top =
      preferredTop + contentRect.height <= viewportHeight - padding
        ? preferredTop
        : Math.max(padding, fallbackTop);

    const preferredLeft =
      align === "start" ? triggerRect.left : triggerRect.right - contentRect.width;
    const left = Math.min(
      Math.max(padding, preferredLeft),
      viewportWidth - contentRect.width - padding,
    );

    setPosition({ top, left });
  }, [align, contentRef, sideOffset, triggerRef]);

  useEffect(() => {
    if (!open) return;
    updatePosition();

    const onPointerDown = (event: MouseEvent) => {
      const target = event.target as Node;
      const clickedTrigger = triggerRef.current?.contains(target);
      const clickedContent = contentRef.current?.contains(target);
      if (!clickedTrigger && !clickedContent) {
        setOpen(false);
      }
    };

    const onEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };

    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);
    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onEscape);

    return () => {
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onEscape);
    };
  }, [contentRef, open, setOpen, updatePosition, triggerRef]);

  if (!open) return null;

  return createPortal(
    <div
      ref={contentRef}
      role="dialog"
      aria-modal={false}
      className={cn(
        "fixed z-50 rounded-lg border border-border bg-popover p-2 text-popover-foreground shadow-md",
        className,
      )}
      style={{ top: position.top, left: position.left }}
      {...props}
    >
      {children}
    </div>,
    document.body,
  );
}
