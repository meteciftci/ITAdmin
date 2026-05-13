import { useTranslation } from "react-i18next";

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
      <DialogContent role="alertdialog" aria-modal="true">
        <DialogHeader>
          <DialogTitle>{t("auth:sessionTimeout.title")}</DialogTitle>
          <DialogDescription>
            {t("auth:sessionTimeout.description", { seconds: safeSeconds })}
          </DialogDescription>
        </DialogHeader>
        <div className="px-4 py-3 text-center">
          <span
            aria-live="polite"
            className="inline-flex items-baseline gap-2 text-3xl font-semibold tabular-nums"
          >
            {safeSeconds}
            <span className="text-sm font-normal text-muted-foreground">s</span>
          </span>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onSignOut} disabled={isExtending}>
            {t("auth:sessionTimeout.signOut")}
          </Button>
          <Button onClick={onContinue} disabled={isExtending}>
            {isExtending
              ? t("auth:sessionTimeout.continueLoading")
              : t("auth:sessionTimeout.continue")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
