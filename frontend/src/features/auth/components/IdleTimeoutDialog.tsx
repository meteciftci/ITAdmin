import { useTranslation } from "react-i18next";
import { ShieldAlert } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

type IdleTimeoutDialogProps = {
  open: boolean;
  remainingSeconds: number;
  isExtending: boolean;
  onContinue: () => void;
  onSignOut: () => void;
};

export function IdleTimeoutDialog({
  open,
  remainingSeconds,
  isExtending,
  onContinue,
  onSignOut,
}: IdleTimeoutDialogProps) {
  const { t } = useTranslation(["auth"]);
  const safeSeconds = Math.max(0, Math.ceil(remainingSeconds));

  return (
    <Dialog open={open}>
      <DialogContent
        role="alertdialog"
        aria-modal="true"
        className="max-w-md overflow-hidden p-0"
      >
        <DialogHeader className="border-b-0 px-5 pb-2 pt-5 text-center">
          <div className="mx-auto mb-3 flex size-14 items-center justify-center rounded-2xl bg-primary/10 text-primary ring-1 ring-primary/15">
            <ShieldAlert className="size-7" aria-hidden="true" />
          </div>
          <DialogTitle className="text-lg">{t("auth:sessionTimeout.title")}</DialogTitle>
          <DialogDescription className="mx-auto max-w-sm leading-relaxed">
            {t("auth:sessionTimeout.description", { seconds: safeSeconds })}
          </DialogDescription>
        </DialogHeader>
        <div className="px-5 py-4">
          <div className="rounded-xl border bg-muted/40 px-4 py-4 text-center shadow-inner">
            <span
              aria-live="polite"
              className="inline-flex items-baseline gap-2 text-5xl font-semibold tracking-tight tabular-nums"
            >
              {safeSeconds}
              <span className="text-sm font-medium text-muted-foreground">
                {t("auth:sessionTimeout.secondsShort")}
              </span>
            </span>
          </div>
        </div>
        <DialogFooter className="flex-col gap-2 border-t bg-muted/20 px-5 py-4 sm:flex-row sm:justify-end">
          <Button
            variant="outline"
            className="w-full sm:w-auto"
            onClick={onSignOut}
            disabled={isExtending}
          >
            {t("auth:sessionTimeout.signOut")}
          </Button>
          <Button className="w-full sm:w-auto" onClick={onContinue} disabled={isExtending}>
            {isExtending
              ? t("auth:sessionTimeout.continueLoading")
              : t("auth:sessionTimeout.continue")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
