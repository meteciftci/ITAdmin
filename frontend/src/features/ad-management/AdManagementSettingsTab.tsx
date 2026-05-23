import { useCallback, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
  AD_MANAGEMENT_SETTINGS_QUERY_KEY,
  invalidateAdManagementUserQueries,
  createAdAttributeMapping,
  deleteAdAttributeMapping,
  getAdAttributeMappings,
  getAdManagementSettings,
  updateAdAttributeMapping,
  updateAdManagementSettings,
} from "@/features/ad-management/api";
import { getAdManagementSaveErrorMessage } from "@/features/ad-management/ad-management-save-error";
import { AdAttributeMappingDialog, type AdAttributeMappingDialogFormState } from "@/features/ad-management/components/AdAttributeMappingDialog";
import { AdAttributeMappingsSection } from "@/features/ad-management/components/AdAttributeMappingsSection";
import { AdManagementConnectionForm } from "@/features/ad-management/components/AdManagementConnectionForm";
import { AdManagementNotificationsForm } from "@/features/ad-management/components/AdManagementNotificationsForm";
import { AdUserCreationDefaultsForm } from "@/features/ad-management/components/AdUserCreationDefaultsForm";
import type {
  AdAttributeMapping,
  AdManagementSettings,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";

function buildSettingsKey(settings: AdManagementSettings | undefined): string {
  if (!settings) return "no-settings";
  return [
    settings.isEnabled,
    settings.domainFqdn,
    settings.defaultUserCreationUpnSuffix,
    settings.netbiosDomainName,
    settings.defaultNamingContext,
    settings.baseDn,
    settings.usersRootOu,
    settings.disabledUsersOu,
    settings.groupsSearchBase,
    settings.computersSearchBase,
    settings.preferredDomainControllers.join("|"),
    settings.useSsl,
    settings.ldapPort,
    settings.serviceAccountUserName,
    settings.hasServiceAccountPassword,
    settings.powerShellHealthEnabled,
    settings.powerShellTimeoutSeconds,
  ].join("::");
}

type DialogMode = "create" | "edit";

type DialogState = {
  open: boolean;
  mode: DialogMode;
  initial: AdAttributeMapping | null;
  errorMessage: string | null;
};

type DeleteState = {
  open: boolean;
  mapping: AdAttributeMapping | null;
};

type Props = {
  readOnly: boolean;
};

type AdManagementInnerTab = "connection" | "mappings" | "userCreationDefaults" | "notifications";

export function AdManagementSettingsTab({ readOnly }: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const queryClient = useQueryClient();

  const [activeTab, setActiveTab] = useState<AdManagementInnerTab>("connection");

  const handleTabChange = useCallback((value: string) => {
    setActiveTab(value as AdManagementInnerTab);
  }, []);

  const settingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getAdManagementSettings,
    refetchOnWindowFocus: false,
  });

  const mappingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
    queryFn: getAdAttributeMappings,
    refetchOnWindowFocus: false,
  });

  const updateSettingsMutation = useMutation({
    mutationFn: (payload: UpdateAdManagementSettingsRequest) =>
      updateAdManagementSettings(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
      });
    },
    onError: (error: unknown) => {
      toast.error(
        getAdManagementSaveErrorMessage(
          error,
          t("settings:adManagement.connection.messages.saveFailed"),
        ),
      );
    },
  });

  const saveSettings = (
    payload: UpdateAdManagementSettingsRequest,
    successMessage: string,
  ) => {
    updateSettingsMutation.mutate(payload, {
      onSuccess: () => {
        toast.success(successMessage);
      },
    });
  };

  const [dialog, setDialog] = useState<DialogState>({
    open: false,
    mode: "create",
    initial: null,
    errorMessage: null,
  });

  const [deleteState, setDeleteState] = useState<DeleteState>({
    open: false,
    mapping: null,
  });

  const createMappingMutation = useMutation({
    mutationFn: createAdAttributeMapping,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
      });
      await invalidateAdManagementUserQueries(queryClient);
      toast.success(t("settings:adManagement.mappings.messages.createSuccess"));
      setDialog({ open: false, mode: "create", initial: null, errorMessage: null });
    },
    onError: (error: unknown) => {
      const message = getApiErrorMessage(
        error,
        t("settings:adManagement.mappings.messages.createFailed"),
      );
      setDialog((prev) => ({ ...prev, errorMessage: message }));
    },
  });

  const updateMappingMutation = useMutation({
    mutationFn: (vars: {
      id: string;
      payload: AdAttributeMappingDialogFormState;
    }) =>
      updateAdAttributeMapping(vars.id, {
        displayName: vars.payload.displayName,
        attributeName: vars.payload.attributeName,
        isEnabled: vars.payload.isEnabled,
        isEditable: vars.payload.isEditable,
        isSensitive: vars.payload.isSensitive,
        isSearchable: vars.payload.isSearchable,
        validationType: vars.payload.validationType,
        maskingStrategy: vars.payload.maskingStrategy,
        sortOrder: vars.payload.sortOrder,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
      });
      await invalidateAdManagementUserQueries(queryClient);
      toast.success(t("settings:adManagement.mappings.messages.updateSuccess"));
      setDialog({ open: false, mode: "edit", initial: null, errorMessage: null });
    },
    onError: (error: unknown) => {
      const message = getApiErrorMessage(
        error,
        t("settings:adManagement.mappings.messages.updateFailed"),
      );
      setDialog((prev) => ({ ...prev, errorMessage: message }));
    },
  });

  const deleteMappingMutation = useMutation({
    mutationFn: deleteAdAttributeMapping,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
      });
      await invalidateAdManagementUserQueries(queryClient);
      toast.success(t("settings:adManagement.mappings.messages.deleteSuccess"));
      setDeleteState({ open: false, mapping: null });
    },
    onError: (error: unknown) => {
      toast.error(
        getApiErrorMessage(
          error,
          t("settings:adManagement.mappings.messages.deleteFailed"),
        ),
      );
    },
  });

  const dialogIsSaving =
    createMappingMutation.isPending || updateMappingMutation.isPending;

  function openCreateDialog() {
    setDialog({
      open: true,
      mode: "create",
      initial: null,
      errorMessage: null,
    });
  }

  function openEditDialog(mapping: AdAttributeMapping) {
    setDialog({
      open: true,
      mode: "edit",
      initial: mapping,
      errorMessage: null,
    });
  }

  function handleDialogOpenChange(open: boolean) {
    if (dialogIsSaving) return;
    setDialog((prev) => ({ ...prev, open, errorMessage: open ? prev.errorMessage : null }));
  }

  function handleDialogSubmit(value: AdAttributeMappingDialogFormState) {
    setDialog((prev) => ({ ...prev, errorMessage: null }));
    if (dialog.mode === "create") {
      createMappingMutation.mutate({
        logicalField: value.logicalField,
        displayName: value.displayName,
        attributeName: value.attributeName,
        isEnabled: value.isEnabled,
        isEditable: value.isEditable,
        isSensitive: value.isSensitive,
        isSearchable: value.isSearchable,
        validationType: value.validationType,
        maskingStrategy: value.maskingStrategy,
        sortOrder: value.sortOrder,
      });
      return;
    }

    if (dialog.initial) {
      updateMappingMutation.mutate({
        id: dialog.initial.id,
        payload: value,
      });
    }
  }

  function openDeleteDialog(mapping: AdAttributeMapping) {
    setDeleteState({ open: true, mapping });
  }

  function handleDeleteConfirm() {
    if (!deleteState.mapping) return;
    deleteMappingMutation.mutate(deleteState.mapping.id);
  }

  return (
    <div className="space-y-6">
      <Tabs value={activeTab} onValueChange={handleTabChange}>
        <TabsList className="grid w-full grid-cols-1 sm:grid-cols-2 lg:grid-cols-4">
          <TabsTrigger value="connection">
            {t("settings:adManagement.pageTabs.connection")}
          </TabsTrigger>
          <TabsTrigger value="mappings">
            {t("settings:adManagement.pageTabs.attributeMapping")}
          </TabsTrigger>
          <TabsTrigger value="userCreationDefaults">
            {t("settings:adManagement.pageTabs.userCreationDefaults")}
          </TabsTrigger>
          <TabsTrigger value="notifications">
            {t("settings:adManagement.pageTabs.notifications")}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="connection" className="space-y-2 pt-4">
          <p className="text-xs text-muted-foreground">
            {t("settings:adManagement.connection.description")}
          </p>
          <AdManagementConnectionForm
            key={buildSettingsKey(settingsQuery.data)}
            settings={settingsQuery.data}
            readOnly={readOnly}
            isSaving={updateSettingsMutation.isPending}
            onSave={(payload) =>
              saveSettings(
                payload,
                t("settings:adManagement.connection.messages.saveSuccess"),
              )}
          />
        </TabsContent>

        <TabsContent value="userCreationDefaults" className="pt-4">
          <AdUserCreationDefaultsForm
            key={`${buildSettingsKey(settingsQuery.data)}::defaults`}
            settings={settingsQuery.data}
            readOnly={readOnly}
            isSaving={updateSettingsMutation.isPending}
            onSave={(payload) =>
              saveSettings(
                payload,
                t("settings:adManagement.userCreationDefaults.messages.saveSuccess"),
              )}
          />
        </TabsContent>

        <TabsContent value="notifications" className="pt-4">
          <AdManagementNotificationsForm
            key={`${buildSettingsKey(settingsQuery.data)}::notifications`}
            settings={settingsQuery.data}
            readOnly={readOnly}
            isSaving={updateSettingsMutation.isPending}
            onSave={(payload, meta) => saveSettings(payload, meta.successMessage)}
          />
        </TabsContent>

        <TabsContent value="mappings" className="pt-4">
          <AdAttributeMappingsSection
            mappings={mappingsQuery.data ?? []}
            readOnly={readOnly}
            isLoading={mappingsQuery.isLoading}
            onCreate={openCreateDialog}
            onEdit={openEditDialog}
            onDelete={openDeleteDialog}
          />
        </TabsContent>
      </Tabs>

      <AdAttributeMappingDialog
        open={dialog.open}
        mode={dialog.mode}
        initialValue={dialog.initial}
        isSaving={dialogIsSaving}
        errorMessage={dialog.errorMessage}
        onOpenChange={handleDialogOpenChange}
        onSubmit={handleDialogSubmit}
      />

      <ConfirmDialog
        open={deleteState.open}
        title={t("settings:adManagement.mappings.delete.title")}
        description={
          deleteState.mapping ? (
            <>
              <p>
                {t("settings:adManagement.mappings.delete.description", {
                  logicalField: deleteState.mapping.logicalField,
                  displayName: deleteState.mapping.displayName,
                })}
              </p>
              <p className="mt-2 text-xs text-destructive">
                {t("settings:adManagement.mappings.delete.irreversible")}
              </p>
            </>
          ) : null
        }
        confirmText={t("settings:adManagement.mappings.actions.delete")}
        cancelText={t("common:actions.cancel")}
        variant="danger"
        isLoading={deleteMappingMutation.isPending}
        onConfirm={handleDeleteConfirm}
        onOpenChange={(open) => {
          if (deleteMappingMutation.isPending) return;
          setDeleteState({ open, mapping: open ? deleteState.mapping : null });
        }}
      />
    </div>
  );
}
