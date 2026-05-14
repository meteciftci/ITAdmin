import { useCallback, useState } from "react";

import {
  DIRECTORY_NATIONAL_ID_ATTRIBUTE_KEY,
  SETTING_VALUE_TYPE_STRING,
} from "@/features/settings/settings-constants";
import type {
  ApplicationSetting,
  UpdateApplicationSettingsRequest,
} from "@/features/settings/types";

export type UseDirectorySettingsFormReturn = {
  nationalIdAttribute: string;
  directoryError: string | undefined;
  hydrateFromApplicationSettings: (applicationSettings: ApplicationSetting[]) => void;
  updateNationalIdAttribute: (value: string) => void;
  clearDirectoryError: () => void;
  buildDirectoryPayload: () => UpdateApplicationSettingsRequest;
};

export function useDirectorySettingsForm(): UseDirectorySettingsFormReturn {
  const [nationalIdAttribute, setNationalIdAttribute] = useState("");
  const [directoryError, setDirectoryError] = useState<string | undefined>(undefined);

  const hydrateFromApplicationSettings = useCallback(
    (applicationSettings: ApplicationSetting[]) => {
      const directorySetting = applicationSettings.find(
        (item) => item.key === DIRECTORY_NATIONAL_ID_ATTRIBUTE_KEY,
      );
      setNationalIdAttribute(directorySetting?.value ?? "");
      setDirectoryError(undefined);
    },
    [],
  );

  const updateNationalIdAttribute = useCallback((value: string) => {
    setNationalIdAttribute(value);
  }, []);

  const clearDirectoryError = useCallback(() => {
    setDirectoryError(undefined);
  }, []);

  const buildDirectoryPayload = useCallback((): UpdateApplicationSettingsRequest => {
    const trimmed = nationalIdAttribute.trim();
    return {
      items: [
        {
          key: DIRECTORY_NATIONAL_ID_ATTRIBUTE_KEY,
          value: trimmed === "" ? null : trimmed,
          valueType: SETTING_VALUE_TYPE_STRING,
        },
      ],
    };
  }, [nationalIdAttribute]);

  return {
    nationalIdAttribute,
    directoryError,
    hydrateFromApplicationSettings,
    updateNationalIdAttribute,
    clearDirectoryError,
    buildDirectoryPayload,
  };
}
