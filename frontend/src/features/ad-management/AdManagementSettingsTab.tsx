import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import {
  AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
  AD_MANAGEMENT_SETTINGS_QUERY_KEY,
  createAdAttributeMapping,
  deleteAdAttributeMapping,
  getAdAttributeMappings,
  getAdManagementSettings,
  updateAdAttributeMapping,
  updateAdManagementSettings,
  validateAdManagementSettings,
} from "@/features/ad-management/api";
import { AdAttributeMappingDialog, type AdAttributeMappingDialogFormState } from "@/features/ad-management/components/AdAttributeMappingDialog";
import { AdAttributeMappingsSection } from "@/features/ad-management/components/AdAttributeMappingsSection";
import { AdManagementConnectionForm } from "@/features/ad-management/components/AdManagementConnectionForm";
import type {
  AdAttributeMapping,
  AdManagementSettings,
  AdManagementValidationResult,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { AxiosError } from "axios";

function extractValidationFromError(
  error: unknown,
): AdManagementValidationResult | null {
  if (!(error instanceof AxiosError)) {
    return null;
  }
  const data = error.response?.data;
  if (!data || typeof data !== "object") {
    return null;
  }
  const raw = (data as { validation?: unknown }).validation;
  if (!raw || typeof raw !== "object") {
    return null;
  }
  const candidate = raw as Partial<AdManagementValidationResult> & {
    details?: unknown;
  };
  if (
    typeof candidate.isValid !== "boolean" ||
    typeof candidate.message !== "string" ||
    typeof candidate.checkedAt !== "string" ||
    !Array.isArray(candidate.details)
  ) {
    return null;
  }
  return {
    isValid: candidate.isValid,
    message: candidate.message,
    checkedAt: candidate.checkedAt,
    details: candidate.details
      .map((item) => {
        if (!item || typeof item !== "object") return null;
        const detail = item as Record<string, unknown>;
        if (
          typeof detail.key !== "string" ||
          typeof detail.status !== "string"
        ) {
          return null;
        }
        return {
          key: detail.key,
          status: detail.status,
          message:
            typeof detail.message === "string" ? detail.message : null,
        };
      })
      .filter((d): d is AdManagementValidationResult["details"][number] => d !== null),
  };
}

function buildSettingsKey(settings: AdManagementSettings | undefined): string {
  if (!settings) return "no-settings";
  return [
    settings.isEnabled,
    settings.domainFqdn,
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

export function AdManagementSettingsTab({ readOnly }: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const queryClient = useQueryClient();

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

  const [saveValidationError, setSaveValidationError] =
    useState<AdManagementValidationResult | null>(null);
  const [saveErrorMessage, setSaveErrorMessage] = useState<string | null>(null);

  const updateSettingsMutation = useMutation({
    mutationFn: (payload: UpdateAdManagementSettingsRequest) =>
      updateAdManagementSettings(payload),
    onMutate: () => {
      setSaveValidationError(null);
      setSaveErrorMessage(null);
    },
    onSuccess: async () => {
      setSaveValidationError(null);
      setSaveErrorMessage(null);
      await queryClient.invalidateQueries({
        queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
      });
      toast.success(t("settings:adManagement.connection.messages.saveSuccess"));
    },
    onError: (error: unknown) => {
      const validation = extractValidationFromError(error);
      const fallback = validation
        ? t("settings:adManagement.connection.messages.saveValidationFailed")
        : t("settings:adManagement.connection.messages.saveFailed");
      const message = getApiErrorMessage(error, fallback);
      setSaveValidationError(validation);
      setSaveErrorMessage(message);
      toast.error(message);
    },
  });

  const validateMutation = useMutation({
    mutationFn: validateAdManagementSettings,
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({
        queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
      });
      if (result.isValid) {
        toast.success(t("settings:adManagement.connection.messages.validateSuccess"), {
          description: result.message,
        });
      } else {
        toast.error(t("settings:adManagement.connection.messages.validateFailed"), {
          description: result.message,
        });
      }
    },
    onError: (error: unknown) => {
      toast.error(
        getApiErrorMessage(
          error,
          t("settings:adManagement.connection.messages.validateFailed"),
        ),
      );
    },
  });

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
        validationType: vars.payload.validationType,
        maskingStrategy: vars.payload.maskingStrategy,
        sortOrder: vars.payload.sortOrder,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
      });
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
    <div className="space-y-8">
      <section className="space-y-2">
        <h2 className="text-sm font-semibold">
          {t("settings:adManagement.connection.title")}
        </h2>
        <p className="text-xs text-muted-foreground">
          {t("settings:adManagement.connection.description")}
        </p>
        <AdManagementConnectionForm
          key={buildSettingsKey(settingsQuery.data)}
          settings={settingsQuery.data}
          readOnly={readOnly}
          isSaving={updateSettingsMutation.isPending}
          isValidating={validateMutation.isPending}
          saveValidationError={saveValidationError}
          saveErrorMessage={saveErrorMessage}
          onSave={(payload) => updateSettingsMutation.mutate(payload)}
          onValidate={() => validateMutation.mutate()}
        />
      </section>

      <section className="space-y-2">
        <AdAttributeMappingsSection
          mappings={mappingsQuery.data ?? []}
          readOnly={readOnly}
          isLoading={mappingsQuery.isLoading}
          onCreate={openCreateDialog}
          onEdit={openEditDialog}
          onDelete={openDeleteDialog}
        />
      </section>

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
