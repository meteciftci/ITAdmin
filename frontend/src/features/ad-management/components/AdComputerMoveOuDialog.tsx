import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { getParentDistinguishedName } from "@/features/ad-management/ad-ldap-dn";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import type { AdComputerDetail } from "@/features/ad-management/types";
import { getAdComputerPrimaryLabel } from "@/features/ad-management/ad-computer-display-labels";
import { cn } from "@/lib/utils";

type Props = {
  open: boolean;
  computer: AdComputerDetail;
  isSaving: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (targetOuDistinguishedName: string) => void;
};

function SummaryField({
  label,
  value,
}: {
  label: string;
  value: string | null | undefined;
}) {
  const display = value?.trim() || "-";

  return (
    <div className="space-y-1">
      <div className="text-xs font-medium text-muted-foreground">{label}</div>
      <div className="break-all font-mono text-xs text-muted-foreground" title={display}>
        {display}
      </div>
    </div>
  );
}

export function AdComputerMoveOuDialog({
  open,
  computer,
  isSaving,
  onOpenChange,
  onSubmit,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [targetOuDistinguishedName, setTargetOuDistinguishedName] = useState<string | null>(null);
  const computerLabel = getAdComputerPrimaryLabel(computer);
  const currentParentOu = useMemo(
    () => computer.parentOuDistinguishedName
      ?? getParentDistinguishedName(computer.distinguishedName),
    [computer.distinguishedName, computer.parentOuDistinguishedName],
  );
  const isSameOu = Boolean(
    targetOuDistinguishedName
    && currentParentOu
    && targetOuDistinguishedName.localeCompare(currentParentOu, undefined, { sensitivity: "accent" }) === 0,
  );

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange}>
        <DialogHeader>
          <DialogTitle>{t("adManagement:computers.moveOu.title")}</DialogTitle>
        </DialogHeader>
        <DialogBody className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {t("adManagement:computers.moveOu.description", { name: computerLabel })}
          </p>
          <SummaryField
            label={t("adManagement:computers.moveOu.currentOu")}
            value={currentParentOu}
          />
          <AdOuSearchCombobox
            value={targetOuDistinguishedName}
            onChange={setTargetOuDistinguishedName}
            disabled={isSaving}
            searchContext="computers"
            fieldLabelKey="adManagement:computers.moveOu.targetOu"
            placeholderKey="adManagement:computers.moveOu.targetOuPlaceholder"
            searchKey="adManagement:computers.moveOu.targetOuSearch"
            emptyKey="adManagement:computers.moveOu.targetOuEmpty"
            errorKey="adManagement:computers.moveOu.targetOuLoadFailed"
          />
          {isSameOu ? (
            <FormError message={t("adManagement:computers.moveOu.sameOu")} />
          ) : null}
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
            className={cn(isSameOu && "pointer-events-none")}
            onClick={() => {
              if (targetOuDistinguishedName && !isSameOu) {
                onSubmit(targetOuDistinguishedName);
              }
            }}
            disabled={isSaving || !targetOuDistinguishedName || isSameOu}
          >
            {t("common:actions.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
