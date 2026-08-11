import { useCallback, useState } from "react";
import type { TFunction } from "i18next";

import {
  BRANDING_APPLICATION_NAME_KEY,
  BRANDING_BROWSER_TITLE_KEY,
  BRANDING_FAVICON_URL_KEY,
  BRANDING_FOOTER_TEXT_KEY,
  BRANDING_FOOTER_TEXT_MAX_LENGTH,
  BRANDING_FORGOT_PASSWORD_URL_KEY,
  BRANDING_LOGO_URL_KEY,
  SETTING_VALUE_TYPE_STRING,
} from "@/features/settings/settings-constants";
import type {
  ApplicationSetting,
  BrandingSettings,
  UpdateApplicationSettingsRequest,
} from "@/features/settings/types";

export type UseBrandingSettingsFormNamespaces = readonly ["settings", "common"];

export type UseBrandingSettingsFormParams = {
  t: TFunction<UseBrandingSettingsFormNamespaces>;
};

export type BuildBrandingPayloadParams = {
  logoUrlToPersist: string | null;
  faviconUrlToPersist: string | null;
};

const DEFAULT_APPLICATION_LABEL = "ITAdmin";

export type UseBrandingSettingsFormReturn = {
  brandingApplicationName: string;
  brandingBrowserTitle: string;
  forgotPasswordUrl: string;
  footerText: string;
  forgotPasswordUrlError: string | undefined;
  brandingError: string | undefined;
  applicationNameError: string | undefined;
  browserTitleError: string | undefined;
  footerTextError: string | undefined;
  hydrateFromBranding: (branding: BrandingSettings, applicationSettings?: ApplicationSetting[]) => void;
  updateApplicationName: (value: string) => void;
  updateBrowserTitle: (value: string) => void;
  updateForgotPasswordUrl: (value: string) => void;
  updateFooterText: (value: string) => void;
  clearBrandingError: () => void;
  clearForgotPasswordUrlError: () => void;
  validateBrandingInput: () => boolean;
  validateForgotPasswordUrlInput: () => boolean;
  buildBrandingPayload: (params: BuildBrandingPayloadParams) => UpdateApplicationSettingsRequest;
};

export function useBrandingSettingsForm({
  t,
}: UseBrandingSettingsFormParams): UseBrandingSettingsFormReturn {
  const [brandingApplicationName, setBrandingApplicationName] = useState(DEFAULT_APPLICATION_LABEL);
  const [brandingBrowserTitle, setBrandingBrowserTitle] = useState(DEFAULT_APPLICATION_LABEL);
  const [forgotPasswordUrl, setForgotPasswordUrl] = useState("");
  const [footerText, setFooterText] = useState("");
  const [forgotPasswordUrlError, setForgotPasswordUrlError] = useState<string | undefined>(
    undefined,
  );
  const [brandingError, setBrandingError] = useState<string | undefined>(undefined);
  const [applicationNameError, setApplicationNameError] = useState<string>();
  const [browserTitleError, setBrowserTitleError] = useState<string>();
  const [footerTextError, setFooterTextError] = useState<string>();

  const hydrateFromBranding = useCallback(
    (branding: BrandingSettings, applicationSettings?: ApplicationSetting[]) => {
      setBrandingApplicationName(branding.applicationName ?? DEFAULT_APPLICATION_LABEL);
      setBrandingBrowserTitle(branding.browserTitle ?? DEFAULT_APPLICATION_LABEL);
      setForgotPasswordUrl(branding.forgotPasswordUrl ?? "");
      const rawFooterText = applicationSettings?.find(
        (setting) => setting.key === BRANDING_FOOTER_TEXT_KEY,
      )?.value;
      setFooterText(rawFooterText?.trim() ?? "");
      setForgotPasswordUrlError(undefined);
      setBrandingError(undefined);
      setApplicationNameError(undefined);
      setBrowserTitleError(undefined);
      setFooterTextError(undefined);
    },
    [],
  );

  const clearBrandingError = useCallback(() => {
    setBrandingError(undefined);
    setApplicationNameError(undefined);
    setBrowserTitleError(undefined);
    setFooterTextError(undefined);
  }, []);

  const clearForgotPasswordUrlError = useCallback(() => {
    setForgotPasswordUrlError(undefined);
  }, []);

  const updateApplicationName = useCallback((value: string) => {
    setApplicationNameError(undefined);
    setBrandingApplicationName(value);
  }, []);

  const updateBrowserTitle = useCallback((value: string) => {
    setBrowserTitleError(undefined);
    setBrandingBrowserTitle(value);
  }, []);

  const updateForgotPasswordUrl = useCallback((value: string) => {
    setForgotPasswordUrlError(undefined);
    setForgotPasswordUrl(value);
  }, []);

  const updateFooterText = useCallback((value: string) => {
    setFooterTextError(undefined);
    setFooterText(value);
  }, []);

  const validateBrandingInput = useCallback((): boolean => {
    let valid = true;
    setApplicationNameError(undefined);
    setBrowserTitleError(undefined);
    setFooterTextError(undefined);
    if (!brandingApplicationName.trim()) {
      setApplicationNameError(t("settings:application.validation.applicationNameRequired"));
      valid = false;
    }
    if (brandingApplicationName.trim().length > 100) {
      setApplicationNameError(t("settings:application.validation.applicationNameMax"));
      valid = false;
    }

    if (!brandingBrowserTitle.trim()) {
      setBrowserTitleError(t("settings:application.validation.browserTitleRequired"));
      valid = false;
    }
    if (brandingBrowserTitle.trim().length > 100) {
      setBrowserTitleError(t("settings:application.validation.browserTitleMax"));
      valid = false;
    }

    if (footerText.trim().length > BRANDING_FOOTER_TEXT_MAX_LENGTH) {
      setFooterTextError(t("settings:application.validation.footerTextMax"));
      valid = false;
    }

    return valid;
  }, [brandingApplicationName, brandingBrowserTitle, footerText, t]);

  const validateForgotPasswordUrlInput = useCallback((): boolean => {
    const trimmed = forgotPasswordUrl.trim();
    if (!trimmed) {
      setForgotPasswordUrlError(undefined);
      return true;
    }

    if (!/^https?:\/\//i.test(trimmed) || trimmed.length > 500) {
      setForgotPasswordUrlError(t("settings:application.validation.forgotPasswordUrlInvalid"));
      return false;
    }

    setForgotPasswordUrlError(undefined);
    return true;
  }, [forgotPasswordUrl, t]);

  const buildBrandingPayload = useCallback(
    ({
      logoUrlToPersist,
      faviconUrlToPersist,
    }: BuildBrandingPayloadParams): UpdateApplicationSettingsRequest => {
      const trimmedForgotPasswordUrl = forgotPasswordUrl.trim();
      return {
        items: [
          {
            key: BRANDING_APPLICATION_NAME_KEY,
            value: brandingApplicationName.trim() || DEFAULT_APPLICATION_LABEL,
            valueType: SETTING_VALUE_TYPE_STRING,
          },
          {
            key: BRANDING_BROWSER_TITLE_KEY,
            value: brandingBrowserTitle.trim() || DEFAULT_APPLICATION_LABEL,
            valueType: SETTING_VALUE_TYPE_STRING,
          },
          {
            key: BRANDING_LOGO_URL_KEY,
            value: logoUrlToPersist,
            valueType: SETTING_VALUE_TYPE_STRING,
          },
          {
            key: BRANDING_FAVICON_URL_KEY,
            value: faviconUrlToPersist,
            valueType: SETTING_VALUE_TYPE_STRING,
          },
          {
            key: BRANDING_FORGOT_PASSWORD_URL_KEY,
            value: trimmedForgotPasswordUrl === "" ? "" : trimmedForgotPasswordUrl,
            valueType: SETTING_VALUE_TYPE_STRING,
          },
          {
            key: BRANDING_FOOTER_TEXT_KEY,
            value: footerText.trim() === "" ? "" : footerText.trim(),
            valueType: SETTING_VALUE_TYPE_STRING,
          },
        ],
      };
    },
    [brandingApplicationName, brandingBrowserTitle, footerText, forgotPasswordUrl],
  );

  return {
    brandingApplicationName,
    brandingBrowserTitle,
    forgotPasswordUrl,
    footerText,
    forgotPasswordUrlError,
    brandingError,
    applicationNameError,
    browserTitleError,
    footerTextError,
    hydrateFromBranding,
    updateApplicationName,
    updateBrowserTitle,
    updateForgotPasswordUrl,
    updateFooterText,
    clearBrandingError,
    clearForgotPasswordUrlError,
    validateBrandingInput,
    validateForgotPasswordUrlInput,
    buildBrandingPayload,
  };
}
