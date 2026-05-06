import type { ReactNode } from "react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

type LogDetailDialogRow = {
  label: string;
  value?: ReactNode;
};

type LogDetailDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  rows: LogDetailDialogRow[];
  description?: string | null;
  descriptionLabel: string;
  closeLabel: string;
};

export function LogDetailDialog({
  open,
  onOpenChange,
  title,
  rows,
  description,
  descriptionLabel,
  closeLabel,
}: LogDetailDialogProps) {
  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange} className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{descriptionLabel}</DialogDescription>
        </DialogHeader>

        <div className="max-h-[70vh] space-y-4 overflow-y-auto p-4 text-sm">
          <div className="grid gap-3 md:grid-cols-2">
            {rows.map((row) => (
              <div key={row.label} className="space-y-1">
                <p className="text-xs text-muted-foreground">{row.label}</p>
                <div className="min-h-5 whitespace-pre-wrap break-words">
                  {row.value ?? "-"}
                </div>
              </div>
            ))}
          </div>

          <div className="space-y-1 border-t pt-3">
            <p className="text-xs text-muted-foreground">{descriptionLabel}</p>
            <div className="rounded-md border bg-muted/30 p-3 whitespace-pre-wrap break-words">
              {description || "-"}
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {closeLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
