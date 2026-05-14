import { useCallback, useEffect, useState } from "react";
import type { TFunction } from "i18next";
import { toast } from "sonner";

import {
  MAX_FAVICON_BYTES,
  MAX_LOGO_BYTES,
} from "@/features/settings/settings-constants";
import type { BrandingSettings } from "@/features/settings/types";

/** i18n namespaces used by branding asset validation toasts (align with SettingsPage / branding form). */
export type UseBrandingAssetSettingsFormNamespaces = readonly ["settings", "common"];

export type UseBrandingAssetSettingsFormParams = {
  t: TFunction<UseBrandingAssetSettingsFormNamespaces>;
};

export type UseBrandingAssetSettingsFormReturn = {
  brandingLogoUrl: string | null;
  logoFile: File | null;
  logoPreviewUrl: string | null;
  selectedLogoFileName: string | null;
  brandingFaviconUrl: string | null;
  faviconFile: File | null;
  faviconPreviewUrl: string | null;
  selectedFaviconFileName: string | null;
  hydrateAssetUrlsFromBranding: (branding: BrandingSettings) => void;
  handleLogoSelect: (file: File | null) => Promise<void>;
  handleFaviconSelect: (file: File | null) => Promise<void>;
  resetSelectedAssetsAfterSave: () => void;
  clearSelectedAssets: () => void;
};

async function validateLogoFile(
  file: File,
  t: TFunction<UseBrandingAssetSettingsFormNamespaces>,
): Promise<boolean> {
  const extension = file.name.split(".").pop()?.toLowerCase();
  if (!extension || !["png", "jpg", "jpeg"].includes(extension)) {
    toast.error(t("settings:application.validation.logoType"));
    return false;
  }

  if (file.size > MAX_LOGO_BYTES) {
    toast.error(t("settings:application.validation.logoSize"));
    return false;
  }

  const objectUrl = URL.createObjectURL(file);
  const dimensionsValid = await new Promise<boolean>((resolve) => {
    const image = new Image();
    image.onload = () => {
      const ok =
        image.naturalWidth >= 32 &&
        image.naturalHeight >= 32 &&
        image.naturalWidth <= 512 &&
        image.naturalHeight <= 512;
      resolve(ok);
    };
    image.onerror = () => resolve(false);
    image.src = objectUrl;
  });
  URL.revokeObjectURL(objectUrl);

  if (!dimensionsValid) {
    toast.error(t("settings:application.validation.logoDimensions"));
    return false;
  }

  return true;
}

async function validateFaviconFile(
  file: File,
  t: TFunction<UseBrandingAssetSettingsFormNamespaces>,
): Promise<boolean> {
  const extension = file.name.split(".").pop()?.toLowerCase();
  if (!extension || !["png", "jpg", "jpeg", "ico"].includes(extension)) {
    toast.error(t("settings:application.validation.faviconType"));
    return false;
  }

  if (file.size > MAX_FAVICON_BYTES) {
    toast.error(t("settings:application.validation.faviconSize"));
    return false;
  }

  if (extension === "ico") {
    return true;
  }

  const objectUrl = URL.createObjectURL(file);
  const dimensionsValid = await new Promise<boolean>((resolve) => {
    const image = new Image();
    image.onload = () => {
      const ok =
        image.naturalWidth >= 16 &&
        image.naturalHeight >= 16 &&
        image.naturalWidth <= 512 &&
        image.naturalHeight <= 512;
      resolve(ok);
    };
    image.onerror = () => resolve(false);
    image.src = objectUrl;
  });
  URL.revokeObjectURL(objectUrl);

  if (!dimensionsValid) {
    toast.error(t("settings:application.validation.faviconDimensions"));
    return false;
  }

  return true;
}

export function useBrandingAssetSettingsForm({
  t,
}: UseBrandingAssetSettingsFormParams): UseBrandingAssetSettingsFormReturn {
  const [brandingLogoUrl, setBrandingLogoUrl] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoPreviewUrl, setLogoPreviewUrl] = useState<string | null>(null);
  const [selectedLogoFileName, setSelectedLogoFileName] = useState<string | null>(null);
  const [brandingFaviconUrl, setBrandingFaviconUrl] = useState<string | null>(null);
  const [faviconFile, setFaviconFile] = useState<File | null>(null);
  const [faviconPreviewUrl, setFaviconPreviewUrl] = useState<string | null>(null);
  const [selectedFaviconFileName, setSelectedFaviconFileName] = useState<string | null>(null);

  const clearSelectedAssets = useCallback(() => {
    setLogoPreviewUrl((prev) => {
      if (prev) {
        URL.revokeObjectURL(prev);
      }
      return null;
    });
    setFaviconPreviewUrl((prev) => {
      if (prev) {
        URL.revokeObjectURL(prev);
      }
      return null;
    });
    setLogoFile(null);
    setFaviconFile(null);
    setSelectedLogoFileName(null);
    setSelectedFaviconFileName(null);
  }, []);

  const hydrateAssetUrlsFromBranding = useCallback((branding: BrandingSettings) => {
    setLogoPreviewUrl((prev) => {
      if (prev) {
        URL.revokeObjectURL(prev);
      }
      return null;
    });
    setFaviconPreviewUrl((prev) => {
      if (prev) {
        URL.revokeObjectURL(prev);
      }
      return null;
    });
    setLogoFile(null);
    setFaviconFile(null);
    setSelectedLogoFileName(null);
    setSelectedFaviconFileName(null);
    setBrandingLogoUrl(branding.logoUrl ?? null);
    setBrandingFaviconUrl(branding.faviconUrl ?? null);
  }, []);

  const resetSelectedAssetsAfterSave = useCallback(() => {
    clearSelectedAssets();
  }, [clearSelectedAssets]);

  useEffect(() => {
    return () => {
      if (logoPreviewUrl) {
        URL.revokeObjectURL(logoPreviewUrl);
      }
    };
  }, [logoPreviewUrl]);

  useEffect(() => {
    return () => {
      if (faviconPreviewUrl) {
        URL.revokeObjectURL(faviconPreviewUrl);
      }
    };
  }, [faviconPreviewUrl]);

  const handleLogoSelect = useCallback(
    async (file: File | null) => {
      if (!file) {
        setSelectedLogoFileName(null);
        return;
      }

      const valid = await validateLogoFile(file, t);
      if (!valid) {
        setSelectedLogoFileName(null);
        return;
      }

      setLogoPreviewUrl((prev) => {
        if (prev) {
          URL.revokeObjectURL(prev);
        }
        return URL.createObjectURL(file);
      });
      setLogoFile(file);
      setSelectedLogoFileName(file.name);
    },
    [t],
  );

  const handleFaviconSelect = useCallback(
    async (file: File | null) => {
      if (!file) {
        setSelectedFaviconFileName(null);
        return;
      }

      const valid = await validateFaviconFile(file, t);
      if (!valid) {
        setSelectedFaviconFileName(null);
        return;
      }

      setFaviconPreviewUrl((prev) => {
        if (prev) {
          URL.revokeObjectURL(prev);
        }
        return URL.createObjectURL(file);
      });
      setFaviconFile(file);
      setSelectedFaviconFileName(file.name);
    },
    [t],
  );

  return {
    brandingLogoUrl,
    logoFile,
    logoPreviewUrl,
    selectedLogoFileName,
    brandingFaviconUrl,
    faviconFile,
    faviconPreviewUrl,
    selectedFaviconFileName,
    hydrateAssetUrlsFromBranding,
    handleLogoSelect,
    handleFaviconSelect,
    resetSelectedAssetsAfterSave,
    clearSelectedAssets,
  };
}
