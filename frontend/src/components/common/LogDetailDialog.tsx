import type { ReactNode } from "react";
import { CircleAlert, LoaderCircle } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
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
  isLoading?: boolean;
  loadingLabel?: string;
  error?: ReactNode;
  actions?: ReactNode;
};

export function LogDetailDialog({
  open,
  onOpenChange,
  title,
  rows,
  description,
  descriptionLabel,
  closeLabel,
  isLoading = false,
  loadingLabel,
  error,
  actions,
}: LogDetailDialogProps) {
  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange} className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{descriptionLabel}</DialogDescription>
        </DialogHeader>

        <DialogBody className="max-h-[70vh] overflow-y-auto text-sm">
          {isLoading ? (
            <div
              className="flex items-center gap-2 rounded-lg border bg-muted/30 px-3 py-2 text-muted-foreground"
              role="status"
            >
              <LoaderCircle className="size-4 animate-spin" aria-hidden />
              <span>{loadingLabel}</span>
            </div>
          ) : null}

          {error ? (
            <div
              className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-destructive"
              role="alert"
            >
              <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span>{error}</span>
            </div>
          ) : null}

          <dl className="grid gap-4 md:grid-cols-2">
            {rows.map((row) => (
              <div key={row.label} className="space-y-1">
                <dt className="text-xs font-medium text-muted-foreground">{row.label}</dt>
                <dd className="min-h-5 whitespace-pre-wrap break-words">
                  {row.value ?? "-"}
                </dd>
              </div>
            ))}
          </dl>

          <div className="space-y-1 border-t pt-3">
            <p className="text-xs text-muted-foreground">{descriptionLabel}</p>
            <div className="rounded-md border bg-muted/30 p-3 whitespace-pre-wrap break-words">
              {description || "-"}
            </div>
          </div>
        </DialogBody>

        <DialogFooter className="flex-col sm:flex-row sm:items-center sm:justify-between">
          {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : <span />}
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {closeLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
