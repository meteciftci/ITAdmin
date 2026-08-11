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

type PopoverDescendantRegistry = {
  register: (node: HTMLElement) => () => void;
};

const PopoverDescendantContext = createContext<PopoverDescendantRegistry | null>(null);

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
  fullWidth?: boolean;
  className?: string;
};

export function PopoverTrigger({ asChild, children, fullWidth = false, className }: PopoverTriggerProps) {
  const { open, setOpen, triggerRef } = usePopoverContext();
  const onClick = () => setOpen(!open);
  const wrapperClassName = cn(fullWidth ? "flex w-full" : "inline-flex", className);

  if (asChild && isValidElement(children)) {
    const child = children as ReactElement<{
      onClick?: () => void;
      "aria-haspopup"?: "dialog";
      "aria-expanded"?: boolean;
    }>;
    return (
      <span ref={triggerRef} className={wrapperClassName}>
        {cloneElement(child, {
          onClick: () => {
            child.props.onClick?.();
            onClick();
          },
          "aria-haspopup": "dialog",
          "aria-expanded": open,
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
      aria-haspopup="dialog"
      aria-expanded={open}
    >
      {children}
    </button>
  );
}

type PopoverContentProps = HTMLAttributes<HTMLDivElement> & {
  align?: "start" | "end";
  sideOffset?: number;
  matchTriggerWidth?: boolean;
};

export function PopoverContent({
  className,
  children,
  align = "start",
  sideOffset = 8,
  matchTriggerWidth = false,
  style,
  ...props
}: PopoverContentProps) {
  const { open, setOpen, triggerRef, contentRef } = usePopoverContext();
  const parentRegistry = useContext(PopoverDescendantContext);
  const descendantsRef = useRef<Set<HTMLElement>>(new Set());
  const [position, setPosition] = useState<{ top: number; left: number; width?: number }>({
    top: 0,
    left: 0,
  });

  // Allow nested popovers (rendered into a portal as DOM siblings) to register
  // their content nodes so ancestor popovers do not treat inner clicks as
  // outside clicks. Registration propagates up the chain to support any depth.
  const descendantRegistry = useMemo<PopoverDescendantRegistry>(
    () => ({
      register: (node: HTMLElement) => {
        descendantsRef.current.add(node);
        const unregisterFromParent = parentRegistry?.register(node);
        return () => {
          descendantsRef.current.delete(node);
          unregisterFromParent?.();
        };
      },
    }),
    [parentRegistry],
  );

  useEffect(() => {
    if (!open) return;
    const node = contentRef.current;
    if (!node || !parentRegistry) return;
    return parentRegistry.register(node);
  }, [open, parentRegistry, contentRef]);

  const updatePosition = useCallback(() => {
    if (!triggerRef.current || !contentRef.current) return;

    const triggerRect = triggerRef.current.getBoundingClientRect();
    const contentRect = contentRef.current.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const padding = 8;
    const contentWidth = matchTriggerWidth ? triggerRect.width : contentRect.width;

    const preferredTop = triggerRect.bottom + sideOffset;
    const fallbackTop = triggerRect.top - contentRect.height - sideOffset;
    const top =
      preferredTop + contentRect.height <= viewportHeight - padding
        ? preferredTop
        : Math.max(padding, fallbackTop);

    const preferredLeft =
      align === "start" ? triggerRect.left : triggerRect.right - contentWidth;
    const left = Math.min(
      Math.max(padding, preferredLeft),
      viewportWidth - contentWidth - padding,
    );

    setPosition({
      top,
      left,
      width: matchTriggerWidth ? triggerRect.width : undefined,
    });
  }, [align, contentRef, matchTriggerWidth, sideOffset, triggerRef]);

  useEffect(() => {
    if (!open) return;

    updatePosition();
    const frameId = requestAnimationFrame(() => updatePosition());

    const onPointerDown = (event: MouseEvent) => {
      const target = event.target as Node;
      const clickedTrigger = triggerRef.current?.contains(target);
      const clickedContent = contentRef.current?.contains(target);
      const clickedDescendant = Array.from(descendantsRef.current).some((node) =>
        node.contains(target),
      );
      if (!clickedTrigger && !clickedContent && !clickedDescendant) {
        setOpen(false);
      }
    };

    const onEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        setOpen(false);
        window.setTimeout(() => {
          const trigger = triggerRef.current;
          if (trigger?.matches("button, [href], [tabindex]")) {
            trigger.focus();
            return;
          }
          trigger?.querySelector<HTMLElement>("button, [href], [tabindex]")?.focus();
        }, 0);
      }
    };

    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);
    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onEscape);

    return () => {
      cancelAnimationFrame(frameId);
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onEscape);
    };
  }, [contentRef, open, setOpen, updatePosition, triggerRef]);

  if (!open) return null;

  return createPortal(
    <PopoverDescendantContext.Provider value={descendantRegistry}>
      <div
        ref={contentRef}
        role="dialog"
        aria-modal={false}
        data-popover-content=""
        className={cn(
          "fixed z-50 rounded-lg border border-border bg-popover p-2 text-popover-foreground shadow-md",
          className,
        )}
        style={{
          ...style,
          top: position.top,
          left: position.left,
          ...(position.width !== undefined ? { width: position.width } : {}),
        }}
        {...props}
      >
        {children}
      </div>
    </PopoverDescendantContext.Provider>,
    document.body,
  );
}
