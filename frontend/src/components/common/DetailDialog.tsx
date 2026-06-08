import type { ReactNode } from "react";

import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

type DetailDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
};

export function DetailDialog({
  open,
  onOpenChange,
  title,
  description,
  actions,
  children,
}: DetailDialogProps) {
  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange} className="max-w-3xl">
        <DialogHeader className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0 space-y-1.5">
            <DialogTitle>{title}</DialogTitle>
            {description ? <DialogDescription>{description}</DialogDescription> : null}
          </div>
          {actions ? (
            <div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
              {actions}
            </div>
          ) : null}
        </DialogHeader>
        <DialogBody className="max-h-[70vh] overflow-y-auto">{children}</DialogBody>
      </DialogContent>
    </Dialog>
  );
}
