import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

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
  deleteAdOrganizationalUnit,
  invalidateAdOrganizationalUnitQueries,
} from "@/features/ad-management/api";
import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";
import { formatAdOrganizationalUnitCount } from "@/features/ad-management/ad-ou-display-labels";
import type { AdOrganizationalUnitDetail, AdOrganizationalUnitManageListItem } from "@/features/ad-management/types";

type DeleteProps = {
  open: boolean;
  organizationalUnit: AdOrganizationalUnitManageListItem | AdOrganizationalUnitDetail | null;
  onOpenChange: (open: boolean) => void;
  onDeleted?: () => void;
};

export function AdDeleteOrganizationalUnitDialog({
  open,
  organizationalUnit,
  onOpenChange,
  onDeleted,
}: DeleteProps) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const emptyText = t("common:notAvailable");

  const deleteMutation = useMutation({
    mutationFn: () => {
      if (!organizationalUnit) {
        throw new Error("Missing organizational unit");
      }

      return deleteAdOrganizationalUnit(organizationalUnit.objectGuid);
    },
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(
          resolveAdManagementApiMessage(t, response, "adManagement:organizationalUnits.delete.error"),
        );
        return;
      }

      await invalidateAdOrganizationalUnitQueries(queryClient);
      toast.success(t("adManagement:organizationalUnits.delete.success"));
      onOpenChange(false);
      onDeleted?.();
    },
    onError: (error) => {
      toast.error(getAdManagementApiErrorMessage(error, t, "adManagement:organizationalUnits.delete.error"));
    },
  });

  const summary = organizationalUnit
    ? "contentSummary" in organizationalUnit
      ? organizationalUnit.contentSummary
      : {
          childOuCount: organizationalUnit.childOuCount,
          userCount: organizationalUnit.userCount,
          groupCount: organizationalUnit.groupCount,
          computerCount: organizationalUnit.computerCount,
        }
    : null;

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange}>
        <DialogHeader>
          <DialogTitle>{t("adManagement:organizationalUnits.delete.title")}</DialogTitle>
          <DialogDescription>{t("adManagement:organizationalUnits.delete.description")}</DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-3">
          {summary ? (
            <div className="rounded-md border p-3 text-sm">
              <p className="font-medium">{t("adManagement:organizationalUnits.delete.contentSummary")}</p>
              <ul className="mt-2 space-y-1 text-muted-foreground">
                <li>
                  {t("adManagement:organizationalUnits.summary.childOuCount", {
                    count: formatAdOrganizationalUnitCount(summary.childOuCount, emptyText),
                  })}
                </li>
                <li>
                  {t("adManagement:organizationalUnits.summary.userCount", {
                    count: formatAdOrganizationalUnitCount(summary.userCount, emptyText),
                  })}
                </li>
                <li>
                  {t("adManagement:organizationalUnits.summary.groupCount", {
                    count: formatAdOrganizationalUnitCount(summary.groupCount, emptyText),
                  })}
                </li>
                <li>
                  {t("adManagement:organizationalUnits.summary.computerCount", {
                    count: formatAdOrganizationalUnitCount(summary.computerCount, emptyText),
                  })}
                </li>
              </ul>
            </div>
          ) : null}
          <p className="text-sm text-destructive">{t("adManagement:organizationalUnits.delete.warning")}</p>
        </DialogBody>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={deleteMutation.isPending}
            onClick={() => deleteMutation.mutate()}
          >
            {t("common:actions.delete")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
