import { useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { buildUpdateAdManagementSettingsPayload } from "@/features/ad-management/ad-management-settings-payload";
import { AdOuPickerField } from "@/features/ad-management/components/AdOuPickerField";
import type {
  AdManagementSettings,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";

type Props = {
  settings: AdManagementSettings | undefined;
  readOnly: boolean;
  isSaving: boolean;
  onSave: (payload: UpdateAdManagementSettingsRequest) => void;
};

export function AdManagementScopesForm({
  settings,
  readOnly,
  isSaving,
  onSave,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const [usersRootOu, setUsersRootOu] = useState(settings?.usersRootOu ?? null);
  const [disabledUsersOu, setDisabledUsersOu] = useState(settings?.disabledUsersOu ?? null);
  const [groupsSearchBase, setGroupsSearchBase] = useState(settings?.groupsSearchBase ?? null);
  const [computersSearchBase, setComputersSearchBase] = useState(
    settings?.computersSearchBase ?? null,
  );

  function handleSave() {
    if (!settings || readOnly) {
      return;
    }

    onSave(
      buildUpdateAdManagementSettingsPayload(settings, {
        usersRootOu,
        disabledUsersOu,
        groupsSearchBase,
        computersSearchBase,
      }),
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-sm font-semibold">
          {t("settings:adManagement.scopes.title")}
        </h3>
        <p className="mt-1 text-xs text-muted-foreground">
          {t("settings:adManagement.scopes.description")}
        </p>
      </div>

      <div className="grid max-w-2xl gap-4">
        <AdOuPickerField
          value={usersRootOu}
          onChange={setUsersRootOu}
          searchContext="users"
          allowClear
          required={false}
          disabled={readOnly || isSaving}
          fieldLabelKey="settings:adManagement.scopes.fields.usersRootOu"
          placeholderKey="settings:adManagement.scopes.fields.usersRootOuPlaceholder"
          searchKey="settings:adManagement.scopes.fields.usersRootOuSearch"
          emptyKey="settings:adManagement.scopes.empty.ouNotFound"
          errorKey="settings:adManagement.scopes.errors.ouLoadFailed"
        />
        <p className="-mt-2 text-xs text-muted-foreground">
          {t("settings:adManagement.scopes.fields.usersRootOuHelp")}
        </p>

        <AdOuPickerField
          value={disabledUsersOu}
          onChange={setDisabledUsersOu}
          searchContext="users"
          allowClear
          required={false}
          disabled={readOnly || isSaving}
          fieldLabelKey="settings:adManagement.scopes.fields.disabledUsersOu"
          placeholderKey="settings:adManagement.scopes.fields.disabledUsersOuPlaceholder"
          searchKey="settings:adManagement.scopes.fields.disabledUsersOuSearch"
          emptyKey="settings:adManagement.scopes.empty.ouNotFound"
          errorKey="settings:adManagement.scopes.errors.ouLoadFailed"
        />
        <p className="-mt-2 text-xs text-muted-foreground">
          {t("settings:adManagement.scopes.fields.disabledUsersOuHelp")}
        </p>

        <AdOuPickerField
          value={groupsSearchBase}
          onChange={setGroupsSearchBase}
          searchContext="groups"
          allowClear
          required={false}
          disabled={readOnly || isSaving}
          fieldLabelKey="settings:adManagement.scopes.fields.groupsSearchBase"
          placeholderKey="settings:adManagement.scopes.fields.groupsSearchBasePlaceholder"
          searchKey="settings:adManagement.scopes.fields.groupsSearchBaseSearch"
          emptyKey="settings:adManagement.scopes.empty.ouNotFound"
          errorKey="settings:adManagement.scopes.errors.ouLoadFailed"
        />
        <p className="-mt-2 text-xs text-muted-foreground">
          {t("settings:adManagement.scopes.fields.groupsSearchBaseHelp")}
        </p>

        <AdOuPickerField
          value={computersSearchBase}
          onChange={setComputersSearchBase}
          searchContext="computers"
          allowClear
          required={false}
          disabled={readOnly || isSaving}
          fieldLabelKey="settings:adManagement.scopes.fields.computersSearchBase"
          placeholderKey="settings:adManagement.scopes.fields.computersSearchBasePlaceholder"
          searchKey="settings:adManagement.scopes.fields.computersSearchBaseSearch"
          emptyKey="settings:adManagement.scopes.empty.ouNotFound"
          errorKey="settings:adManagement.scopes.errors.ouLoadFailed"
        />
        <p className="-mt-2 text-xs text-muted-foreground">
          {t("settings:adManagement.scopes.fields.computersSearchBaseHelp")}
        </p>
      </div>

      {!readOnly ? (
        <Button type="button" onClick={handleSave} disabled={!settings || isSaving}>
          {t("common:actions.save")}
        </Button>
      ) : null}
    </div>
  );
}
