import {
  createContext,
  useCallback,
  useContext,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactNode,
} from "react";

import { cn } from "@/lib/utils";

type TabsContextValue = {
  baseId: string;
  value: string;
  setValue: (value: string) => void;
  registerTrigger: (value: string, element: HTMLButtonElement | null) => void;
  focusTriggerByOffset: (currentValue: string, offset: number) => void;
  getTabId: (value: string) => string;
  getPanelId: (value: string) => string;
};

const TabsContext = createContext<TabsContextValue | null>(null);

const useTabsContext = (component: string): TabsContextValue => {
  const context = useContext(TabsContext);
  if (!context) {
    throw new Error(`${component} must be used inside <Tabs>.`);
  }
  return context;
};

type TabsProps = {
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
  className?: string;
  children: ReactNode;
};

export function Tabs({
  value: controlledValue,
  defaultValue,
  onValueChange,
  className,
  children,
}: TabsProps) {
  const baseId = useId();
  const [internalValue, setInternalValue] = useState<string>(defaultValue ?? "");
  const triggers = useRef<Map<string, HTMLButtonElement>>(new Map());
  const order = useRef<string[]>([]);

  const isControlled = controlledValue !== undefined;
  const value = isControlled ? controlledValue : internalValue;

  const setValue = useCallback(
    (next: string) => {
      if (!isControlled) {
        setInternalValue(next);
      }
      onValueChange?.(next);
    },
    [isControlled, onValueChange],
  );

  const registerTrigger = useCallback(
    (triggerValue: string, element: HTMLButtonElement | null) => {
      if (element) {
        triggers.current.set(triggerValue, element);
        if (!order.current.includes(triggerValue)) {
          order.current.push(triggerValue);
        }
      } else {
        triggers.current.delete(triggerValue);
        order.current = order.current.filter((entry) => entry !== triggerValue);
      }
    },
    [],
  );

  const focusTriggerByOffset = useCallback(
    (currentValue: string, offset: number) => {
      const list = order.current;
      if (list.length === 0) return;
      const currentIndex = list.indexOf(currentValue);
      if (currentIndex === -1) return;
      const nextIndex = (currentIndex + offset + list.length) % list.length;
      const nextValue = list[nextIndex];
      const nextElement = triggers.current.get(nextValue);
      if (nextElement) {
        nextElement.focus();
        setValue(nextValue);
      }
    },
    [setValue],
  );

  const contextValue = useMemo<TabsContextValue>(
    () => ({
      baseId,
      value,
      setValue,
      registerTrigger,
      focusTriggerByOffset,
      getTabId: (triggerValue: string) => `${baseId}-tab-${triggerValue}`,
      getPanelId: (triggerValue: string) => `${baseId}-panel-${triggerValue}`,
    }),
    [baseId, focusTriggerByOffset, registerTrigger, setValue, value],
  );

  return (
    <TabsContext.Provider value={contextValue}>
      <div className={cn("flex flex-col gap-4", className)} data-slot="tabs">
        {children}
      </div>
    </TabsContext.Provider>
  );
}

type TabsListProps = {
  className?: string;
  children: ReactNode;
};

export function TabsList({ className, children }: TabsListProps) {
  return (
    <div className="overflow-x-auto">
      <div
        role="tablist"
        className={cn(
          "inline-flex w-full min-w-max items-center gap-1 rounded-lg bg-muted/60 p-1 text-sm",
          className,
        )}
        data-slot="tabs-list"
      >
        {children}
      </div>
    </div>
  );
}

type TabsTriggerProps = {
  value: string;
  disabled?: boolean;
  className?: string;
  children: ReactNode;
};

export function TabsTrigger({ value, disabled, className, children }: TabsTriggerProps) {
  const ctx = useTabsContext("TabsTrigger");
  const isActive = ctx.value === value;

  const handleKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === "ArrowRight") {
      event.preventDefault();
      ctx.focusTriggerByOffset(value, 1);
    } else if (event.key === "ArrowLeft") {
      event.preventDefault();
      ctx.focusTriggerByOffset(value, -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      ctx.focusTriggerByOffset(value, -Number.MAX_SAFE_INTEGER);
    } else if (event.key === "End") {
      event.preventDefault();
      ctx.focusTriggerByOffset(value, Number.MAX_SAFE_INTEGER);
    }
  };

  return (
    <button
      type="button"
      role="tab"
      id={ctx.getTabId(value)}
      aria-selected={isActive}
      aria-controls={ctx.getPanelId(value)}
      tabIndex={isActive ? 0 : -1}
      disabled={disabled}
      ref={(element) => ctx.registerTrigger(value, element)}
      onClick={() => ctx.setValue(value)}
      onKeyDown={handleKeyDown}
      data-slot="tabs-trigger"
      data-state={isActive ? "active" : "inactive"}
      className={cn(
        "inline-flex flex-1 items-center justify-center whitespace-nowrap rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
        "disabled:pointer-events-none disabled:opacity-50",
        isActive
          ? "bg-primary text-primary-foreground shadow-sm"
          : "text-muted-foreground hover:bg-muted hover:text-foreground",
        className,
      )}
    >
      {children}
    </button>
  );
}

type TabsContentProps = {
  value: string;
  className?: string;
  children: ReactNode;
};

export function TabsContent({ value, className, children }: TabsContentProps) {
  const ctx = useTabsContext("TabsContent");
  const isActive = ctx.value === value;

  if (!isActive) {
    return null;
  }

  return (
    <div
      role="tabpanel"
      id={ctx.getPanelId(value)}
      aria-labelledby={ctx.getTabId(value)}
      data-slot="tabs-content"
      data-state="active"
      className={cn("focus-visible:outline-none", className)}
    >
      {children}
    </div>
  );
}
