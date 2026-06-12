import { useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

type Props = {
  open: boolean;
  initialDescription: string | null;
  isSaving: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (description: string | null) => void;
};

export function AdComputerUpdateDescriptionDialog({
  open,
  initialDescription,
  isSaving,
  onOpenChange,
  onSubmit,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [description, setDescription] = useState(initialDescription ?? "");

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange}>
        <DialogHeader>
          <DialogTitle>{t("adManagement:computers.updateDescription.title")}</DialogTitle>
        </DialogHeader>
        <DialogBody className="space-y-3">
          <div className="space-y-1.5">
            <Label htmlFor="computer-description">
              {t("adManagement:computers.updateDescription.fieldLabel")}
            </Label>
            <Textarea
              id="computer-description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              disabled={isSaving}
              rows={4}
            />
            <p className="text-xs text-muted-foreground">
              {t("adManagement:computers.updateDescription.clearHint")}
            </p>
          </div>
        </DialogBody>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isSaving}
          >
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            onClick={() => onSubmit(description.trim() ? description.trim() : null)}
            disabled={isSaving}
          >
            {t("common:actions.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
