import * as React from "react"

import { cn } from "@/lib/utils"

type DialogProps = {
  open: boolean
  children: React.ReactNode
}

function Dialog({ open, children }: DialogProps) {
  if (!open) return null
  return <>{children}</>
}

type DialogContentProps = React.ComponentProps<"div"> & {
  onOpenChange?: (open: boolean) => void
}

function DialogContent({
  className,
  children,
  onOpenChange,
  ...props
}: DialogContentProps) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onClick={() => onOpenChange?.(false)}
    >
      <div
        data-slot="dialog-content"
        className={cn(
          "max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-xl border bg-card text-card-foreground shadow-lg",
          className
        )}
        onClick={(event) => event.stopPropagation()}
        {...props}
      >
        {children}
      </div>
    </div>
  )
}

function DialogHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-header"
      className={cn("space-y-1 border-b px-4 py-3", className)}
      {...props}
    />
  )
}

function DialogTitle({ className, ...props }: React.ComponentProps<"h2">) {
  return (
    <h2
      data-slot="dialog-title"
      className={cn("text-base font-semibold", className)}
      {...props}
    />
  )
}

function DialogDescription({
  className,
  ...props
}: React.ComponentProps<"p">) {
  return (
    <p
      data-slot="dialog-description"
      className={cn("text-sm text-muted-foreground", className)}
      {...props}
    />
  )
}

function DialogFooter({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-footer"
      className={cn("flex justify-end gap-2 border-t px-4 py-3", className)}
      {...props}
    />
  )
}

export {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
}
