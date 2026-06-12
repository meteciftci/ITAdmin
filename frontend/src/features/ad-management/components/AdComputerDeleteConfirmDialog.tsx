import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

type Props = {
  open: boolean;
  computerId: string;
  computerLabel: string;
  samAccountName?: string | null;
  isDeleting: boolean;
  onConfirm: () => void;
  onOpenChange: (open: boolean) => void;
};

function resolveConfirmationValue(
  computerLabel: string,
  samAccountName?: string | null,
): string {
  return samAccountName?.trim() || computerLabel.trim();
}

export function AdComputerDeleteConfirmDialog({
  open,
  computerId,
  computerLabel,
  samAccountName,
  isDeleting,
  onConfirm,
  onOpenChange,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [confirmValue, setConfirmValue] = useState("");

  const expectedConfirmValue = useMemo(
    () => resolveConfirmationValue(computerLabel, samAccountName),
    [computerLabel, samAccountName],
  );

  const isConfirmMatch =
    expectedConfirmValue.length > 0
    && confirmValue.trim().toLowerCase() === expectedConfirmValue.toLowerCase();

  return (
    <Dialog open={open}>
      <DialogContent
        key={`delete-computer-${computerId}-${open ? "open" : "closed"}`}
        onOpenChange={(nextOpen) => {
          if (!isDeleting) {
            if (!nextOpen) {
              setConfirmValue("");
            }
            onOpenChange(nextOpen);
          }
        }}
      >
        <DialogHeader>
          <DialogTitle>{t("adManagement:computers.delete.title")}</DialogTitle>
          <DialogDescription>
            {t("adManagement:computers.delete.description", { name: computerLabel })}
          </DialogDescription>
        </DialogHeader>

        <DialogBody>
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              {t("adManagement:computers.delete.impact")}
            </p>

            <div className="space-y-2">
              <Label htmlFor="delete-computer-confirm">
                {t("adManagement:computers.delete.confirmLabel")}
              </Label>
              <Input
                id="delete-computer-confirm"
                value={confirmValue}
                onChange={(event) => setConfirmValue(event.target.value)}
                placeholder={t("adManagement:computers.delete.confirmPlaceholder", {
                  name: expectedConfirmValue,
                })}
                autoComplete="off"
                disabled={isDeleting}
              />
              <p className="text-xs text-muted-foreground">
                {t("adManagement:computers.delete.confirmHint", {
                  name: expectedConfirmValue,
                })}
              </p>
            </div>
          </div>
        </DialogBody>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => {
              setConfirmValue("");
              onOpenChange(false);
            }}
            disabled={isDeleting}
          >
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={onConfirm}
            disabled={!isConfirmMatch || isDeleting}
          >
            {t("common:actions.delete")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
