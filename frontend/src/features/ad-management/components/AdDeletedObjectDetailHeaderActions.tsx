import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  adDetailActionButtonSizingClass,
  adDetailOutlineButtonClass,
} from "@/features/ad-management/ad-user-detail-button-styles";
import { canRestoreDeletedObject } from "@/features/ad-management/ad-deleted-object-restore-eligibility";
import { AdDeletedObjectRestoreConfirmDialog } from "@/features/ad-management/components/AdDeletedObjectRestoreConfirmDialog";
import {
  invalidateAdManagementDeletedObjectRestoreQueries,
  restoreAdDeletedObject,
} from "@/features/ad-management/api";
import type { AdDeletedObjectDetail } from "@/features/ad-management/types";
import { canAccess } from "@/lib/permissions";
import { getApiErrorMessage } from "@/lib/api-error";
import { useAuthStore } from "@/features/auth/auth-store";

type Props = {
  detail: AdDeletedObjectDetail;
  returnPath: string;
  isFetching: boolean;
  onRefresh: () => void;
};

export function AdDeletedObjectDetailHeaderActions({
  detail,
  returnPath,
  isFetching,
  onRefresh,
}: Props) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const [isRestoreDialogOpen, setIsRestoreDialogOpen] = useState(false);

  const canRestore =
    canAccess(user, "AdManagement.DeletedObjects.Restore")
    && canRestoreDeletedObject(detail);

  const restoreMutation = useMutation({
    mutationFn: () => restoreAdDeletedObject(detail.id),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(t("adManagement:deletedObjects.errors.restoreFailed"));
        return;
      }

      await invalidateAdManagementDeletedObjectRestoreQueries(queryClient);
      toast.success(response.message || t("adManagement:deletedObjects.success.restore"));
      setIsRestoreDialogOpen(false);
      navigate(returnPath);
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("adManagement:deletedObjects.errors.restoreFailed")));
    },
  });

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <Link to={returnPath} className={adDetailOutlineButtonClass}>
          {t("common:actions.back")}
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
        {canRestore ? (
          <Button
            type="button"
            size="sm"
            className={adDetailActionButtonSizingClass}
            onClick={() => setIsRestoreDialogOpen(true)}
          >
            {t("adManagement:deletedObjects.actions.restore")}
          </Button>
        ) : null}
      </div>

      <AdDeletedObjectRestoreConfirmDialog
        open={isRestoreDialogOpen}
        target={detail}
        isRestoring={restoreMutation.isPending}
        onOpenChange={setIsRestoreDialogOpen}
        onConfirm={() => restoreMutation.mutate()}
      />
    </>
  );
}
