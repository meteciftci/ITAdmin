import type { ReactNode } from "react";

import {
  Dialog,
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
  children: ReactNode;
};

export function DetailDialog({
  open,
  onOpenChange,
  title,
  description,
  children,
}: DetailDialogProps) {
  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange} className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description ? <DialogDescription>{description}</DialogDescription> : null}
        </DialogHeader>
        <div className="max-h-[70vh] space-y-4 overflow-y-auto p-4">{children}</div>
      </DialogContent>
    </Dialog>
  );
}
