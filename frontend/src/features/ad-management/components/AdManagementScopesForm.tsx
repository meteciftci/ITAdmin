import { useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { buildUpdateAdManagementSettingsPayload } from "@/features/ad-management/ad-management-settings-payload";
import { AdSettingsOuPickerField } from "@/features/ad-management/components/AdSettingsOuPickerField";
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

  const disabled = readOnly || isSaving;

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
    <div className="space-y-6">
      <div>
        <h3 className="text-sm font-semibold">
          {t("settings:adManagement.scopes.title")}
        </h3>
        <p className="mt-1 text-xs text-muted-foreground">
          {t("settings:adManagement.scopes.description")}
        </p>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <AdSettingsOuPickerField
          value={usersRootOu}
          onChange={setUsersRootOu}
          disabled={disabled}
          labelKey="settings:adManagement.scopes.fields.usersRootOu"
          descriptionKey="settings:adManagement.scopes.fields.usersRootOuHelp"
          placeholderKey="settings:adManagement.scopes.fields.usersRootOuPlaceholder"
        />
        <AdSettingsOuPickerField
          value={disabledUsersOu}
          onChange={setDisabledUsersOu}
          disabled={disabled}
          labelKey="settings:adManagement.scopes.fields.disabledUsersOu"
          descriptionKey="settings:adManagement.scopes.fields.disabledUsersOuHelp"
          placeholderKey="settings:adManagement.scopes.fields.disabledUsersOuPlaceholder"
        />
        <AdSettingsOuPickerField
          value={groupsSearchBase}
          onChange={setGroupsSearchBase}
          disabled={disabled}
          labelKey="settings:adManagement.scopes.fields.groupsSearchBase"
          descriptionKey="settings:adManagement.scopes.fields.groupsSearchBaseHelp"
          placeholderKey="settings:adManagement.scopes.fields.groupsSearchBasePlaceholder"
        />
        <AdSettingsOuPickerField
          value={computersSearchBase}
          onChange={setComputersSearchBase}
          disabled={disabled}
          labelKey="settings:adManagement.scopes.fields.computersSearchBase"
          descriptionKey="settings:adManagement.scopes.fields.computersSearchBaseHelp"
          placeholderKey="settings:adManagement.scopes.fields.computersSearchBasePlaceholder"
        />
      </div>

      {!readOnly ? (
        <div className="flex justify-end border-t pt-4">
          <Button type="button" onClick={handleSave} disabled={!settings || isSaving}>
            {t("common:actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
