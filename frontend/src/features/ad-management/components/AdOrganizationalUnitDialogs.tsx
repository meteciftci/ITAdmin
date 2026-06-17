import { useState } from "react";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  deleteAdOrganizationalUnit,
  invalidateAdOrganizationalUnitQueries,
  moveAdOrganizationalUnit,
  renameAdOrganizationalUnit,
} from "@/features/ad-management/api";
import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";
import { formatAdOrganizationalUnitCount } from "@/features/ad-management/ad-ou-display-labels";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";
import { isInvalidOrganizationalUnitMoveTarget } from "@/features/ad-management/ad-ldap-dn";
import type { AdOrganizationalUnitDetail, AdOrganizationalUnitManageListItem } from "@/features/ad-management/types";

type RenameProps = {
  open: boolean;
  organizationalUnit: AdOrganizationalUnitManageListItem | AdOrganizationalUnitDetail | null;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
};

function resolveOrganizationalUnitRenameName(
  organizationalUnit: AdOrganizationalUnitManageListItem | AdOrganizationalUnitDetail,
): string {
  return organizationalUnit.ou?.trim() || organizationalUnit.name?.trim() || "";
}

export function AdRenameOrganizationalUnitDialog({
  open,
  organizationalUnit,
  onOpenChange,
  onSuccess,
}: RenameProps) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const [name, setName] = useState("");

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen && organizationalUnit) {
      setName(resolveOrganizationalUnitRenameName(organizationalUnit));
    }
    onOpenChange(nextOpen);
  };

  const renameMutation = useMutation({
    mutationFn: () => {
      if (!organizationalUnit) {
        throw new Error("Missing organizational unit");
      }

      return renameAdOrganizationalUnit(organizationalUnit.objectGuid, { name: name.trim() });
    },
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(
          resolveAdManagementApiMessage(t, response, "adManagement:organizationalUnits.rename.error"),
        );
        return;
      }

      await invalidateAdOrganizationalUnitQueries(queryClient);
      toast.success(t("adManagement:organizationalUnits.rename.success"));
      handleOpenChange(false);
      onSuccess?.();
    },
    onError: (error) => {
      toast.error(getAdManagementApiErrorMessage(error, t, "adManagement:organizationalUnits.rename.error"));
    },
  });

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={handleOpenChange}>
        <DialogHeader>
          <DialogTitle>{t("adManagement:organizationalUnits.rename.title")}</DialogTitle>
          <DialogDescription>{t("adManagement:organizationalUnits.rename.description")}</DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="rename-ou-name">{t("adManagement:organizationalUnits.fields.name")}</Label>
            <Input
              id="rename-ou-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </div>
        </DialogBody>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            disabled={!name.trim() || renameMutation.isPending}
            onClick={() => renameMutation.mutate()}
          >
            {t("common:actions.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

type MoveProps = {
  open: boolean;
  organizationalUnit: AdOrganizationalUnitManageListItem | AdOrganizationalUnitDetail | null;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
};

export function AdMoveOrganizationalUnitDialog({
  open,
  organizationalUnit,
  onOpenChange,
  onSuccess,
}: MoveProps) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const [targetParentDistinguishedName, setTargetParentDistinguishedName] = useState<string | null>(null);

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      setTargetParentDistinguishedName(null);
    }
    onOpenChange(nextOpen);
  };

  const isInvalidTarget =
    Boolean(organizationalUnit)
    && Boolean(targetParentDistinguishedName)
    && isInvalidOrganizationalUnitMoveTarget(
      organizationalUnit!.distinguishedName,
      targetParentDistinguishedName!,
    );

  const moveMutation = useMutation({
    mutationFn: () => {
      if (!organizationalUnit || !targetParentDistinguishedName?.trim()) {
        throw new Error("Missing move target");
      }

      return moveAdOrganizationalUnit(organizationalUnit.objectGuid, {
        targetParentDistinguishedName: targetParentDistinguishedName.trim(),
      });
    },
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(
          resolveAdManagementApiMessage(t, response, "adManagement:organizationalUnits.move.error"),
        );
        return;
      }

      await invalidateAdOrganizationalUnitQueries(queryClient);
      toast.success(t("adManagement:organizationalUnits.move.success"));
      handleOpenChange(false);
      onSuccess?.();
    },
    onError: (error) => {
      toast.error(getAdManagementApiErrorMessage(error, t, "adManagement:organizationalUnits.move.error"));
    },
  });

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={handleOpenChange}>
        <DialogHeader>
          <DialogTitle>{t("adManagement:organizationalUnits.move.title")}</DialogTitle>
          <DialogDescription>{t("adManagement:organizationalUnits.move.description")}</DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-4">
          <AdOuSearchCombobox
            value={targetParentDistinguishedName}
            onChange={setTargetParentDistinguishedName}
            searchContext="manage"
            fieldLabelKey="adManagement:organizationalUnits.fields.targetParent"
            placeholderKey="adManagement:organizationalUnits.fields.targetParentPlaceholder"
            searchKey="adManagement:organizationalUnits.fields.parentSearch"
            emptyKey="adManagement:organizationalUnits.empty.notFound"
            errorKey="adManagement:organizationalUnits.errors.loadFailed"
            excludeDistinguishedName={organizationalUnit?.distinguishedName ?? null}
          />
          {isInvalidTarget ? (
            <p className="text-sm text-destructive">
              {t("adManagement:organizationalUnits.move.invalidTarget")}
            </p>
          ) : null}
        </DialogBody>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            disabled={!targetParentDistinguishedName?.trim() || isInvalidTarget || moveMutation.isPending}
            onClick={() => moveMutation.mutate()}
          >
            {t("adManagement:organizationalUnits.actions.move")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

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
                    count: formatAdOrganizationalUnitCount(summary.childOuCount),
                  })}
                </li>
                <li>
                  {t("adManagement:organizationalUnits.summary.userCount", {
                    count: formatAdOrganizationalUnitCount(summary.userCount),
                  })}
                </li>
                <li>
                  {t("adManagement:organizationalUnits.summary.groupCount", {
                    count: formatAdOrganizationalUnitCount(summary.groupCount),
                  })}
                </li>
                <li>
                  {t("adManagement:organizationalUnits.summary.computerCount", {
                    count: formatAdOrganizationalUnitCount(summary.computerCount),
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
