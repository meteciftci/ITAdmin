import * as React from "react"
import { Dialog as BaseDialog } from "@base-ui/react/dialog"

import { cn } from "@/lib/utils"

type DialogProps = {
  open: boolean
  children: React.ReactNode
}

type DialogOpenChangeHandler = (open: boolean) => void

const DialogOpenChangeContext = React.createContext<
  React.MutableRefObject<DialogOpenChangeHandler | undefined> | null
>(null)

function Dialog({ open, children }: DialogProps) {
  const onOpenChangeRef = React.useRef<DialogOpenChangeHandler | undefined>(undefined)

  return (
    <BaseDialog.Root
      open={open}
      onOpenChange={(nextOpen) => onOpenChangeRef.current?.(nextOpen)}
    >
      <DialogOpenChangeContext.Provider value={onOpenChangeRef}>
        {children}
      </DialogOpenChangeContext.Provider>
    </BaseDialog.Root>
  )
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
  const onOpenChangeRef = React.useContext(DialogOpenChangeContext)

  React.useEffect(() => {
    if (!onOpenChangeRef) return
    onOpenChangeRef.current = onOpenChange

    return () => {
      if (onOpenChangeRef.current === onOpenChange) {
        onOpenChangeRef.current = undefined
      }
    }
  }, [onOpenChange, onOpenChangeRef])

  return (
    <BaseDialog.Portal>
      <BaseDialog.Backdrop className="fixed inset-0 z-50 bg-black/45 backdrop-blur-[1px]" />
      <BaseDialog.Viewport className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto p-4">
        <BaseDialog.Popup
          data-slot="dialog-content"
          className={cn(
            "max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-xl border bg-card text-card-foreground shadow-xl outline-none",
            "focus-visible:ring-3 focus-visible:ring-ring/25",
            className
          )}
          {...props}
        >
          {children}
        </BaseDialog.Popup>
      </BaseDialog.Viewport>
    </BaseDialog.Portal>
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

function DialogTitle({ className, ...props }: React.ComponentProps<typeof BaseDialog.Title>) {
  return (
    <BaseDialog.Title
      data-slot="dialog-title"
      className={cn("text-base font-semibold", className)}
      {...props}
    />
  )
}

function DialogDescription({
  className,
  ...props
}: React.ComponentProps<typeof BaseDialog.Description>) {
  return (
    <BaseDialog.Description
      data-slot="dialog-description"
      className={cn("text-sm text-muted-foreground", className)}
      {...props}
    />
  )
}

function DialogBody({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-body"
      className={cn("space-y-4 px-4 py-4", className)}
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
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
}
