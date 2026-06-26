import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { buildUpdateAdManagementSettingsPayload } from "@/features/ad-management/ad-management-settings-payload";
import {
  AD_UPN_SUFFIXES_QUERY_KEY,
  getAdUpnSuffixes,
} from "@/features/ad-management/api";
import { AdSettingsOuPickerField } from "@/features/ad-management/components/AdSettingsOuPickerField";
import { isAdManagementConnectionReady } from "@/features/ad-management/is-ad-management-connection-ready";
import { isSavedDefaultMissingFromAdList } from "@/features/ad-management/resolve-default-upn-suffix";
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

export function AdCreationDefaultsForm({
  settings,
  readOnly,
  isSaving,
  onSave,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const [defaultUserOu, setDefaultUserOu] = useState(settings?.defaultUserOu ?? null);
  const [defaultGroupOu, setDefaultGroupOu] = useState(settings?.defaultGroupOu ?? null);
  const [defaultComputerOu, setDefaultComputerOu] = useState(settings?.defaultComputerOu ?? null);
  const [selectedSuffix, setSelectedSuffix] = useState(
    settings?.defaultUserCreationUpnSuffix ?? "",
  );

  const connectionReady = isAdManagementConnectionReady(settings);
  const disabled = readOnly || isSaving;

  const upnSuffixesQuery = useQuery({
    queryKey: AD_UPN_SUFFIXES_QUERY_KEY,
    queryFn: getAdUpnSuffixes,
    enabled: connectionReady,
  });

  const suffixItems = useMemo(
    () => upnSuffixesQuery.data?.items ?? [],
    [upnSuffixesQuery.data?.items],
  );

  const selectOptions = useMemo(() => {
    const options = [...suffixItems];
    const saved = settings?.defaultUserCreationUpnSuffix?.trim();
    if (
      saved
      && !options.some((item) => item.value.toLowerCase() === saved.toLowerCase())
    ) {
      options.unshift({ value: saved, source: "Saved" });
    }

    return options;
  }, [settings?.defaultUserCreationUpnSuffix, suffixItems]);

  const effectiveValue = selectedSuffix || settings?.defaultUserCreationUpnSuffix || "";

  const savedMissingFromAd = isSavedDefaultMissingFromAdList(
    settings?.defaultUserCreationUpnSuffix,
    suffixItems,
  );

  const listBlocking =
    upnSuffixesQuery.isLoading
    || upnSuffixesQuery.isError
    || (upnSuffixesQuery.isSuccess && suffixItems.length === 0);

  function handleSave() {
    if (!settings || readOnly) {
      return;
    }

    onSave(
      buildUpdateAdManagementSettingsPayload(settings, {
        defaultUserOu,
        defaultGroupOu,
        defaultComputerOu,
        defaultUserCreationUpnSuffix: effectiveValue.trim() || null,
      }),
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-sm font-semibold">
          {t("settings:adManagement.creationDefaults.title")}
        </h3>
        <p className="mt-1 text-xs text-muted-foreground">
          {t("settings:adManagement.creationDefaults.description")}
        </p>
      </div>

      {connectionReady && listBlocking ? (
        <p className="text-sm text-destructive">
          {upnSuffixesQuery.isLoading
            ? t("common:loading")
            : t("settings:adManagement.creationDefaults.errors.suffixListLoadFailed")}
        </p>
      ) : null}

      {connectionReady ? (
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          <AdSettingsOuPickerField
            value={defaultUserOu}
            onChange={setDefaultUserOu}
            disabled={disabled}
            labelKey="settings:adManagement.creationDefaults.fields.defaultUserOu"
            descriptionKey="settings:adManagement.creationDefaults.fields.defaultUserOuHelp"
            placeholderKey="settings:adManagement.creationDefaults.fields.defaultUserOuPlaceholder"
          />
          <AdSettingsOuPickerField
            value={defaultGroupOu}
            onChange={setDefaultGroupOu}
            disabled={disabled}
            labelKey="settings:adManagement.creationDefaults.fields.defaultGroupOu"
            descriptionKey="settings:adManagement.creationDefaults.fields.defaultGroupOuHelp"
            placeholderKey="settings:adManagement.creationDefaults.fields.defaultGroupOuPlaceholder"
          />
          <AdSettingsOuPickerField
            value={defaultComputerOu}
            onChange={setDefaultComputerOu}
            disabled={disabled}
            labelKey="settings:adManagement.creationDefaults.fields.defaultComputerOu"
            descriptionKey="settings:adManagement.creationDefaults.fields.defaultComputerOuHelp"
            placeholderKey="settings:adManagement.creationDefaults.fields.defaultComputerOuPlaceholder"
          />
        </div>
      ) : null}

      {connectionReady && !listBlocking ? (
        <section className="max-w-md space-y-1.5 rounded-lg border bg-card p-4">
          <Label htmlFor="ad-default-user-creation-upn-suffix">
            {t("settings:adManagement.creationDefaults.fields.defaultUpnSuffix")}
          </Label>
          <Select
            id="ad-default-user-creation-upn-suffix"
            value={effectiveValue}
            onChange={(event) => setSelectedSuffix(event.target.value)}
            disabled={disabled}
            className="h-10"
          >
            <option value="">
              {t("settings:adManagement.creationDefaults.fields.none")}
            </option>
            {selectOptions.map((item) => (
              <option key={item.value} value={item.value}>
                {item.value}
              </option>
            ))}
          </Select>
          <p className="text-xs text-muted-foreground">
            {t("settings:adManagement.creationDefaults.fields.defaultUpnSuffixHelp")}
          </p>
          {upnSuffixesQuery.data?.warning ? (
            <p className="text-xs text-amber-600 dark:text-amber-400">
              {upnSuffixesQuery.data.warning}
            </p>
          ) : null}
          {savedMissingFromAd ? (
            <p className="text-xs text-amber-600 dark:text-amber-400">
              {t("settings:adManagement.creationDefaults.warnings.savedSuffixMissingFromAd")}
            </p>
          ) : null}
        </section>
      ) : null}

      {!readOnly ? (
        <div className="flex justify-end border-t pt-4">
          <Button
            type="button"
            onClick={handleSave}
            disabled={!settings || isSaving || !connectionReady || listBlocking}
          >
            {isSaving
              ? t("common:actions.save")
              : t("settings:adManagement.creationDefaults.actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
