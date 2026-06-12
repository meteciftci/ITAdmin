import { useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { RowActions } from "@/components/common/RowActions";
import { Button } from "@/components/ui/button";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/dropdown-menu";
import { isAdComputerAccountOperationRestricted } from "@/features/ad-management/ad-computer-account-guard";
import {
  adDetailActionButtonSizingClass,
  adDetailOutlineButtonClass,
} from "@/features/ad-management/ad-user-detail-button-styles";
import {
  disableAdComputer,
  enableAdComputer,
  invalidateAdManagementComputerQueries,
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
  canEnableComputer: boolean;
  canDisableComputer: boolean;
};

export function AdComputerDetailHeaderActions({
  computer,
  returnPath,
  isFetching,
  onRefresh,
  canEnableComputer,
  canDisableComputer,
}: Props) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const [confirmAction, setConfirmAction] = useState<AdComputerAccountConfirmAction | null>(null);
  const isProtected = isAdComputerAccountOperationRestricted(computer);
  const showEnable = canEnableComputer && !computer.isEnabled && !isProtected;
  const showDisable = canDisableComputer && computer.isEnabled && !isProtected;
  const hasOperations = showEnable || showDisable;
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
        {hasOperations ? (
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
    </>
  );
}
