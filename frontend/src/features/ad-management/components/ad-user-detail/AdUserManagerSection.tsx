import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { AD_USER_FORM_ACTIONS_CLASSNAME } from "@/features/ad-management/ad-form-actions";
import {
  adUserDetailManagerChangeButtonClass,
  adUserDetailManagerClearButtonClass,
} from "@/features/ad-management/ad-user-detail-button-styles";
import { AdUserDetailField } from "@/features/ad-management/components/ad-user-detail/AdUserDetailField";
import { AdUserSearchCombobox } from "@/features/ad-management/components/AdUserSearchCombobox";
import {
  invalidateAdUserDetailRelatedQueries,
  updateAdUserManager,
} from "@/features/ad-management/api";
import type { AdUserDetail, AdUserListItem } from "@/features/ad-management/types";
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";

type Props = {
  user: AdUserDetail;
  canUpdate: boolean;
};

export function AdUserManagerSection({ user, canUpdate }: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [selectedManager, setSelectedManager] = useState<AdUserListItem | null>(null);
  const [clearConfirmOpen, setClearConfirmOpen] = useState(false);

  const hasManager =
    Boolean(user.managerDistinguishedName)
    || Boolean(user.managerDisplayName)
    || Boolean(user.managerSamAccountName);

  const managerMutation = useMutation({
    mutationFn: (payload: { managerUserId: string | null; clearManager: boolean }) =>
      updateAdUserManager(user.id, payload),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(t("adManagement:users.detail.manager.updateFailed"));
        return;
      }

      await invalidateAdUserDetailRelatedQueries(queryClient, user.id);
      toast.success(t("adManagement:users.detail.manager.updated"));
      setEditing(false);
      setSelectedManager(null);
      setClearConfirmOpen(false);
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:users.detail.manager.updateFailed",
        ),
      );
    },
  });

  function handleSave() {
    if (!selectedManager) {
      return;
    }

    managerMutation.mutate({
      managerUserId: selectedManager.id,
      clearManager: false,
    });
  }

  function handleClear() {
    managerMutation.mutate({
      managerUserId: null,
      clearManager: true,
    });
  }

  return (
    <>
      <SectionCard
        title={t("adManagement:users.detail.manager.title")}
        actions={
          canUpdate && hasManager && !editing ? (
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                className={adUserDetailManagerChangeButtonClass}
                onClick={() => setEditing(true)}
              >
                {t("adManagement:users.detail.manager.changeManager")}
              </Button>
              <Button
                type="button"
                className={adUserDetailManagerClearButtonClass}
                onClick={() => setClearConfirmOpen(true)}
              >
                {t("adManagement:users.detail.manager.clearManager")}
              </Button>
            </div>
          ) : canUpdate && !hasManager && !editing ? (
            <Button
              type="button"
              className={adUserDetailManagerChangeButtonClass}
              onClick={() => setEditing(true)}
            >
              {t("adManagement:users.detail.manager.changeManager")}
            </Button>
          ) : null
        }
      >
        {!hasManager && !editing ? (
          <p className="text-sm text-muted-foreground">
            {t("adManagement:users.detail.manager.notAssigned")}
          </p>
        ) : null}

        {hasManager && !editing ? (
          <div className="grid gap-3 md:grid-cols-2">
            <AdUserDetailField
              label={t("adManagement:users.detail.displayName")}
              value={user.managerDisplayName}
            />
            <AdUserDetailField
              label={t("adManagement:users.detail.username")}
              value={user.managerSamAccountName}
            />
            <AdUserDetailField
              label={t("adManagement:users.detail.upn")}
              value={user.managerUserPrincipalName}
            />
            <AdUserDetailField
              label={t("adManagement:users.detail.page.distinguishedName")}
              value={user.managerDistinguishedName}
              valueClassName="break-all font-mono text-xs"
            />
          </div>
        ) : null}

        {editing ? (
          <div className="space-y-4">
            <AdUserSearchCombobox
              value={selectedManager}
              onChange={setSelectedManager}
              excludeUserId={user.id}
              disabled={managerMutation.isPending}
            />
            <div className={AD_USER_FORM_ACTIONS_CLASSNAME}>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  setEditing(false);
                  setSelectedManager(null);
                }}
                disabled={managerMutation.isPending}
              >
                {t("common:actions.cancel")}
              </Button>
              <Button
                type="button"
                size="sm"
                onClick={handleSave}
                disabled={!selectedManager || managerMutation.isPending}
              >
                {t("common:actions.save")}
              </Button>
            </div>
          </div>
        ) : null}
      </SectionCard>

      <ConfirmDialog
        open={clearConfirmOpen}
        onOpenChange={setClearConfirmOpen}
        title={t("adManagement:users.detail.manager.clearManager")}
        description={t("adManagement:users.detail.manager.clearConfirmDescription")}
        confirmText={t("adManagement:users.detail.manager.clearManager")}
        cancelText={t("common:actions.cancel")}
        variant="danger"
        isLoading={managerMutation.isPending}
        onConfirm={handleClear}
      />
    </>
  );
}
