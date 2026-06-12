import { useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { RowActions } from "@/components/common/RowActions";
import { Button } from "@/components/ui/button";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/dropdown-menu";
import { isAdComputerAccountOperationRestricted } from "@/features/ad-management/ad-computer-account-guard";
import { buildAdComputerMoveOuPath } from "@/features/ad-management/ad-computer-detail-path";
import { buildAdComputerDetailReturnState } from "@/features/ad-management/ad-computers-return-path";
import {
  adDetailActionButtonSizingClass,
  adDetailEditButtonClass,
  adDetailOutlineButtonClass,
} from "@/features/ad-management/ad-user-detail-button-styles";
import { AdComputerDeleteConfirmDialog } from "@/features/ad-management/components/AdComputerDeleteConfirmDialog";
import { AdComputerUpdateDescriptionDialog } from "@/features/ad-management/components/AdComputerUpdateDescriptionDialog";
import {
  deleteAdComputer,
  disableAdComputer,
  enableAdComputer,
  invalidateAdManagementComputerQueries,
  updateAdComputer,
} from "@/features/ad-management/api";
import type {
  AdComputerAccountConfirmAction,
  AdComputerDetail,
} from "@/features/ad-management/types";
import { getAdComputerPrimaryLabel } from "@/features/ad-management/ad-computer-display-labels";
import { getApiErrorMessage } from "@/lib/api-error";

type Props = {
  computer: AdComputerDetail;
  returnPath: string;
  isFetching: boolean;
  onRefresh: () => void;
  canUpdateComputer: boolean;
  canMoveOu: boolean;
  canEnableComputer: boolean;
  canDisableComputer: boolean;
  canDeleteComputer: boolean;
};

export function AdComputerDetailHeaderActions({
  computer,
  returnPath,
  isFetching,
  onRefresh,
  canUpdateComputer,
  canMoveOu,
  canEnableComputer,
  canDisableComputer,
  canDeleteComputer,
}: Props) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [confirmAction, setConfirmAction] = useState<AdComputerAccountConfirmAction | null>(null);
  const [isUpdateDialogOpen, setIsUpdateDialogOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const isProtected = isAdComputerAccountOperationRestricted(computer);
  const showUpdate = canUpdateComputer && !isProtected;
  const showMoveOu = canMoveOu && !isProtected;
  const showEnable = canEnableComputer && !computer.isEnabled && !isProtected;
  const showDisable = canDisableComputer && computer.isEnabled && !isProtected;
  const showDelete = canDeleteComputer && !isProtected;
  const hasAccountOperations = showEnable || showDisable || showDelete;
  const computerLabel = getAdComputerPrimaryLabel(computer);

  const accountOperationMutation = useMutation({
    mutationFn: async (action: AdComputerAccountConfirmAction) => {
      if (action === "enable") {
        return enableAdComputer(computer.id);
      }

      return disableAdComputer(computer.id);
    },
    onSuccess: async (response, action) => {
      if (!response.success) {
        toast.error(
          action === "enable"
            ? t("adManagement:computers.messages.enableFailed")
            : t("adManagement:computers.messages.disableFailed"),
        );
        return;
      }

      await invalidateAdManagementComputerQueries(queryClient);

      const message =
        action === "enable"
          ? t("adManagement:computers.messages.enabled")
          : t("adManagement:computers.messages.disabled");
      toast.success(response.message || message);
      setConfirmAction(null);
    },
    onError: (error, action) => {
      toast.error(
        getApiErrorMessage(
          error,
          action === "enable"
            ? t("adManagement:computers.messages.enableFailed")
            : t("adManagement:computers.messages.disableFailed"),
        ),
      );
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteAdComputer(computer.id),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(t("adManagement:computers.delete.error"));
        return;
      }

      await invalidateAdManagementComputerQueries(queryClient);
      toast.success(response.message || t("adManagement:computers.delete.success"));
      setIsDeleteDialogOpen(false);
      navigate(returnPath);
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("adManagement:computers.delete.error")));
    },
  });

  const updateDescriptionMutation = useMutation({
    mutationFn: (description: string | null) =>
      updateAdComputer(computer.id, { description }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(t("adManagement:computers.updateDescription.messages.updateFailed"));
        return;
      }

      await invalidateAdManagementComputerQueries(queryClient);
      toast.success(
        response.message || t("adManagement:computers.updateDescription.messages.updated"),
      );
      setIsUpdateDialogOpen(false);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(
          error,
          t("adManagement:computers.updateDescription.messages.updateFailed"),
        ),
      );
    },
  });

  const confirmCopy = useMemo(() => {
    if (!confirmAction) {
      return { title: "", description: "", variant: "default" as const };
    }

    if (confirmAction === "disable") {
      return {
        title: t("adManagement:computers.confirm.disableTitle"),
        description: t("adManagement:computers.confirm.disableDescription", {
          name: computerLabel,
        }),
        variant: "danger" as const,
      };
    }

    return {
      title: t("adManagement:computers.confirm.enableTitle"),
      description: t("adManagement:computers.confirm.enableDescription", {
        name: computerLabel,
      }),
      variant: "default" as const,
    };
  }, [confirmAction, computerLabel, t]);

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <Link to={returnPath} className={adDetailOutlineButtonClass}>
          {t("adManagement:computers.actions.back")}
        </Link>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className={adDetailActionButtonSizingClass}
          onClick={onRefresh}
          disabled={isFetching}
        >
          {t("common:actions.refresh")}
        </Button>
        {showUpdate ? (
          <Button
            type="button"
            size="sm"
            className={adDetailEditButtonClass}
            onClick={() => setIsUpdateDialogOpen(true)}
          >
            {t("common:actions.edit")}
          </Button>
        ) : null}
        {showMoveOu ? (
          <Button
            type="button"
            variant="outline"
            size="sm"
            className={adDetailActionButtonSizingClass}
            onClick={() =>
              navigate(buildAdComputerMoveOuPath(computer.id), {
                state: buildAdComputerDetailReturnState(computer.id),
              })
            }
          >
            {t("adManagement:computers.actions.moveOu")}
          </Button>
        ) : null}
        {hasAccountOperations ? (
          <RowActions label={t("adManagement:computers.detail.actions.operations")}>
            {showEnable ? (
              <DropdownMenuItem onClick={() => setConfirmAction("enable")}>
                {t("adManagement:computers.actions.enable")}
              </DropdownMenuItem>
            ) : null}
            {showEnable && showDisable ? <DropdownMenuSeparator /> : null}
            {showDisable ? (
              <DropdownMenuItem onClick={() => setConfirmAction("disable")}>
                {t("adManagement:computers.actions.disable")}
              </DropdownMenuItem>
            ) : null}
            {showDelete ? (
              <>
                {(showEnable || showDisable) ? <DropdownMenuSeparator /> : null}
                <DropdownMenuItem
                  className="text-destructive focus:text-destructive"
                  onClick={() => setIsDeleteDialogOpen(true)}
                >
                  {t("common:actions.delete")}
                </DropdownMenuItem>
              </>
            ) : null}
          </RowActions>
        ) : null}
      </div>

      <ConfirmDialog
        open={confirmAction !== null}
        onOpenChange={(open) => {
          if (!open) {
            setConfirmAction(null);
          }
        }}
        title={confirmCopy.title}
        description={confirmCopy.description}
        confirmText={t("common:actions.confirm")}
        cancelText={t("common:actions.cancel")}
        variant={confirmCopy.variant}
        isLoading={accountOperationMutation.isPending}
        onConfirm={() => {
          if (confirmAction) {
            accountOperationMutation.mutate(confirmAction);
          }
        }}
      />

      <AdComputerDeleteConfirmDialog
        open={isDeleteDialogOpen}
        computerId={computer.id}
        computerLabel={computerLabel}
        samAccountName={computer.samAccountName}
        isDeleting={deleteMutation.isPending}
        onOpenChange={setIsDeleteDialogOpen}
        onConfirm={() => deleteMutation.mutate()}
      />

      <AdComputerUpdateDescriptionDialog
        key={`update-${computer.id}-${computer.description ?? ""}-${isUpdateDialogOpen ? "open" : "closed"}`}
        open={isUpdateDialogOpen}
        initialDescription={computer.description}
        isSaving={updateDescriptionMutation.isPending}
        onOpenChange={setIsUpdateDialogOpen}
        onSubmit={(description) => updateDescriptionMutation.mutate(description)}
      />
    </>
  );
}
