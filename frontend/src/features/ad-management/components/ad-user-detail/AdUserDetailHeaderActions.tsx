import { useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { RowActions } from "@/components/common/RowActions";
import { Button } from "@/components/ui/button";
import {
  DropdownMenuItem,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { buildAdUserDetailReturnState } from "@/features/ad-management/ad-return-path";
import { AD_USERS_LIST_PATH } from "@/features/ad-management/ad-users-list-path";
import {
  disableAdUser,
  enableAdUser,
  invalidateAdUserDetailRelatedQueries,
  unlockAdUser,
} from "@/features/ad-management/api";
import type { AdUserAccountConfirmAction, AdUserDetail } from "@/features/ad-management/types";
import {
  adDetailActionButtonSizingClass,
  adDetailEditButtonClass,
  adDetailOutlineButtonClass,
} from "@/features/ad-management/ad-user-detail-button-styles";
import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "@/features/ad-management/ad-management-api-message";

type Props = {
  user: AdUserDetail;
  isFetching: boolean;
  onRefresh: () => void;
  canUpdateUser: boolean;
  canManageGroups: boolean;
  canMoveOu: boolean;
  canEnableUser: boolean;
  canDisableUser: boolean;
  canUnlockUser: boolean;
};

export function AdUserDetailHeaderActions({
  user,
  isFetching,
  onRefresh,
  canUpdateUser,
  canManageGroups,
  canMoveOu,
  canEnableUser,
  canDisableUser,
  canUnlockUser,
}: Props) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [confirmAction, setConfirmAction] = useState<AdUserAccountConfirmAction | null>(null);

  const showEnable = canEnableUser && !user.isEnabled;
  const showDisable = canDisableUser && user.isEnabled;
  const showUnlock = canUnlockUser && user.isLockedOut;
  const hasOperations =
    canManageGroups || canMoveOu || showEnable || showDisable || showUnlock;

  const accountOperationMutation = useMutation({
    mutationFn: async (action: AdUserAccountConfirmAction) => {
      if (action === "enable") {
        return enableAdUser(user.id);
      }

      if (action === "disable") {
        return disableAdUser(user.id);
      }

      return unlockAdUser(user.id);
    },
    onSuccess: async (response, action) => {
      if (!response.success) {
        toast.error(
          resolveAdManagementApiMessage(
            t,
            response,
            "adManagement:users.messages.operationFailed",
          ),
        );
        return;
      }

      await invalidateAdUserDetailRelatedQueries(queryClient, user.id);

      const fallbackKey =
        action === "enable"
          ? "adManagement:users.messages.enabled"
          : action === "disable"
            ? "adManagement:users.messages.disabled"
            : "adManagement:users.messages.unlocked";
      toast.success(resolveAdManagementApiMessage(t, response, fallbackKey));
      setConfirmAction(null);
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:users.messages.operationFailed",
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
        title: t("adManagement:users.confirm.disableTitle"),
        description: t("adManagement:users.confirm.disableDescription"),
        variant: "danger" as const,
      };
    }

    if (confirmAction === "enable") {
      return {
        title: t("adManagement:users.confirm.enableTitle"),
        description: t("adManagement:users.confirm.enableDescription"),
        variant: "default" as const,
      };
    }

    return {
      title: t("adManagement:users.confirm.unlockTitle"),
      description: t("adManagement:users.confirm.unlockDescription"),
      variant: "default" as const,
    };
  }, [confirmAction, t]);

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <Link
          to={AD_USERS_LIST_PATH}
          className={adDetailOutlineButtonClass}
        >
          {t("adManagement:users.detail.page.back")}
        </Link>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className={adDetailActionButtonSizingClass}
          onClick={onRefresh}
          disabled={isFetching}
        >
          {t("adManagement:users.actions.refresh")}
        </Button>
        {canUpdateUser ? (
          <Link
            to={`/ad-management/users/${user.id}/edit`}
            state={buildAdUserDetailReturnState(user.id)}
            className={adDetailEditButtonClass}
          >
            {t("adManagement:users.actions.edit")}
          </Link>
        ) : null}
        {hasOperations ? (
          <RowActions label={t("adManagement:users.detail.actions.operations")}>
            {canManageGroups ? (
              <DropdownMenuItem
                onClick={() =>
                  navigate(`/ad-management/users/${user.id}/groups`, {
                    state: buildAdUserDetailReturnState(user.id),
                  })
                }
              >
                {t("adManagement:users.actions.manageGroups")}
              </DropdownMenuItem>
            ) : null}
            {canMoveOu ? (
              <DropdownMenuItem
                onClick={() =>
                  navigate(`/ad-management/users/${user.id}/move-ou`, {
                    state: buildAdUserDetailReturnState(user.id),
                  })
                }
              >
                {t("adManagement:users.actions.moveOu")}
              </DropdownMenuItem>
            ) : null}
            {showEnable || showDisable || showUnlock ? <DropdownMenuSeparator /> : null}
            {showEnable ? (
              <DropdownMenuItem onClick={() => setConfirmAction("enable")}>
                {t("adManagement:users.detail.actions.enable")}
              </DropdownMenuItem>
            ) : null}
            {showDisable ? (
              <DropdownMenuItem onClick={() => setConfirmAction("disable")}>
                {t("adManagement:users.detail.actions.disable")}
              </DropdownMenuItem>
            ) : null}
            {showUnlock ? (
              <DropdownMenuItem onClick={() => setConfirmAction("unlock")}>
                {t("adManagement:users.detail.actions.unlock")}
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
