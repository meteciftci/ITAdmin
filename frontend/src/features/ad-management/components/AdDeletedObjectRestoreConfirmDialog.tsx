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
import { getAdDeletedObjectPrimaryLabel } from "@/features/ad-management/ad-deleted-object-display-labels";
import { getAdDeletedObjectTypeLabel } from "@/features/ad-management/ad-deleted-object-labels";
import type { AdDeletedObjectDetail, AdDeletedObjectListItem } from "@/features/ad-management/types";

type DeletedObjectRestoreTarget = AdDeletedObjectListItem | AdDeletedObjectDetail;

type Props = {
  open: boolean;
  target: DeletedObjectRestoreTarget | null;
  isRestoring: boolean;
  onConfirm: () => void;
  onOpenChange: (open: boolean) => void;
};

export function AdDeletedObjectRestoreConfirmDialog({
  open,
  target,
  isRestoring,
  onConfirm,
  onOpenChange,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [confirmValue, setConfirmValue] = useState("");

  const primaryLabel = target ? getAdDeletedObjectPrimaryLabel(target) : "";
  const expectedConfirmValue = primaryLabel.trim();

  const isConfirmMatch =
    expectedConfirmValue.length > 0
    && confirmValue.trim().toLowerCase() === expectedConfirmValue.toLowerCase();

  const detailFields = useMemo(() => {
    if (!target) {
      return null;
    }

    return {
      objectType: getAdDeletedObjectTypeLabel(t, target.objectType),
      primaryLabel,
      distinguishedName: target.distinguishedName,
      lastKnownParent: target.lastKnownParent,
      lastKnownRdn: "lastKnownRdn" in target ? target.lastKnownRdn : null,
    };
  }, [primaryLabel, t, target]);

  return (
    <Dialog open={open}>
      <DialogContent
        key={`restore-deleted-object-${target?.id ?? "none"}-${open ? "open" : "closed"}`}
        onOpenChange={(nextOpen) => {
          if (!isRestoring) {
            if (!nextOpen) {
              setConfirmValue("");
            }
            onOpenChange(nextOpen);
          }
        }}
      >
        <DialogHeader>
          <DialogTitle>{t("adManagement:deletedObjects.restore.dialogTitle")}</DialogTitle>
          <DialogDescription>
            {t("adManagement:deletedObjects.restore.dialogDescription")}
          </DialogDescription>
        </DialogHeader>

        <DialogBody>
          <div className="space-y-4">
            {detailFields ? (
              <div className="space-y-3 rounded-lg border bg-muted/20 p-3 text-sm">
                <div className="space-y-1">
                  <p className="text-xs text-muted-foreground">
                    {t("adManagement:deletedObjects.table.type")}
                  </p>
                  <p>{detailFields.objectType}</p>
                </div>
                <div className="space-y-1">
                  <p className="text-xs text-muted-foreground">
                    {t("adManagement:deletedObjects.fields.name")}
                  </p>
                  <p className="break-all">{detailFields.primaryLabel}</p>
                </div>
                <div className="space-y-1">
                  <p className="text-xs text-muted-foreground">
                    {t("adManagement:deletedObjects.fields.distinguishedName")}
                  </p>
                  <p className="break-all font-mono text-xs text-muted-foreground">
                    {detailFields.distinguishedName}
                  </p>
                </div>
                <div className="space-y-1">
                  <p className="text-xs text-muted-foreground">
                    {t("adManagement:deletedObjects.restore.targetLocation")}
                  </p>
                  <p className="break-all font-mono text-xs text-muted-foreground">
                    {detailFields.lastKnownParent?.trim() || "-"}
                  </p>
                </div>
                {detailFields.lastKnownRdn ? (
                  <div className="space-y-1">
                    <p className="text-xs text-muted-foreground">
                      {t("adManagement:deletedObjects.restore.restoredRdn")}
                    </p>
                    <p className="break-all font-mono text-xs text-muted-foreground">
                      {detailFields.lastKnownRdn}
                    </p>
                  </div>
                ) : null}
              </div>
            ) : null}

            <div className="space-y-2">
              <Label htmlFor="restore-deleted-object-confirm">
                {t("adManagement:deletedObjects.restore.confirmLabel")}
              </Label>
              <Input
                id="restore-deleted-object-confirm"
                value={confirmValue}
                onChange={(event) => setConfirmValue(event.target.value)}
                placeholder={expectedConfirmValue}
                autoComplete="off"
                disabled={isRestoring}
              />
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
            disabled={isRestoring}
          >
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            onClick={onConfirm}
            disabled={!isConfirmMatch || isRestoring}
          >
            {t("adManagement:deletedObjects.actions.restore")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
