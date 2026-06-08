import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";
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
import {
  getAdGroupMemberPrimaryLabel,
  getAdGroupMemberSecondaryLabel,
} from "@/features/ad-management/ad-group-display-labels";
import { getAdGroupMemberTypeLabel } from "@/features/ad-management/ad-group-labels";
import type { AdGroupMemberListItem } from "@/features/ad-management/types";

type Props = {
  open: boolean;
  groupName: string | null;
  member: AdGroupMemberListItem | null;
  isLoading: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
};

export function AdRemoveGroupMemberConfirmDialog({
  open,
  groupName,
  member,
  isLoading,
  onOpenChange,
  onConfirm,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);

  const primaryLabel = member ? getAdGroupMemberPrimaryLabel(member) : null;
  const secondaryLabel = member && primaryLabel
    ? getAdGroupMemberSecondaryLabel(member, primaryLabel)
    : null;

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange}>
        <DialogHeader>
          <DialogTitle>{t("adManagement:groups.members.removeTitle")}</DialogTitle>
          <DialogDescription>{t("adManagement:groups.members.removeDescription")}</DialogDescription>
        </DialogHeader>

        <DialogBody>
        {member ? (
          <div className="space-y-3 rounded-md border bg-muted/20 p-3 text-sm">
            <div>
              <p className="text-xs text-muted-foreground">{t("adManagement:groups.table.group")}</p>
              <p className="font-medium">{groupName ?? "-"}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">{t("adManagement:groups.members.selectCandidate")}</p>
              <div className="mt-1 flex flex-wrap items-center gap-2">
                <p className="font-medium">{primaryLabel}</p>
                <Badge variant="outline">{getAdGroupMemberTypeLabel(t, member.type)}</Badge>
              </div>
              {secondaryLabel ? (
                <p className="mt-1 truncate text-xs text-muted-foreground" title={secondaryLabel}>
                  {secondaryLabel}
                </p>
              ) : null}
            </div>
            <div>
              <p className="text-xs text-muted-foreground">
                {t("adManagement:groups.table.distinguishedName")}
              </p>
              <p
                className="break-all font-mono text-xs text-muted-foreground"
                title={member.distinguishedName}
              >
                {member.distinguishedName}
              </p>
            </div>
          </div>
        ) : null}
        </DialogBody>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isLoading}
          >
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={onConfirm}
            disabled={isLoading || !member}
          >
            {t("adManagement:groups.members.remove")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
