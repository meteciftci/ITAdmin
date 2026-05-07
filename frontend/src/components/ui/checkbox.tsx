import * as React from "react"

import { cn } from "@/lib/utils"

function Checkbox({ className, ...props }: React.ComponentProps<"input">) {
  return (
    <input
      type="checkbox"
      data-slot="checkbox"
      className={cn(
        "peer inline-block size-4 shrink-0 cursor-pointer appearance-none rounded-[4px] border border-input bg-background align-middle outline-none transition-colors",
        "hover:border-primary/60",
        "focus-visible:border-primary focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
        "checked:border-primary checked:bg-primary",
        "checked:bg-no-repeat checked:bg-center checked:bg-[length:0.875rem_0.875rem]",
        "checked:bg-[url('data:image/svg+xml;charset=utf-8,%3Csvg%20xmlns=%22http://www.w3.org/2000/svg%22%20viewBox=%220%200%2016%2016%22%20fill=%22none%22%20stroke=%22white%22%20stroke-width=%222.5%22%20stroke-linecap=%22round%22%20stroke-linejoin=%22round%22%3E%3Cpolyline%20points=%223.5%208.5%206.5%2011.5%2012.5%205%22/%3E%3C/svg%3E')]",
        "indeterminate:border-primary indeterminate:bg-primary",
        "disabled:cursor-not-allowed disabled:opacity-50",
        className
      )}
      {...props}
    />
  )
}

export { Checkbox }
