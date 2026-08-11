import type { BrandingSettings } from "@/features/settings/types";

export type BrandingFormSnapshot = {
  applicationName: string;
  browserTitle: string;
  forgotPasswordUrl: string;
  footerText: string;
};

export function isBrandingFormDirty(
  current: BrandingFormSnapshot,
  persisted: BrandingSettings | undefined,
  hasSelectedAsset: boolean,
): boolean {
  if (hasSelectedAsset) return true;
  if (!persisted) return false;

  return (
    current.applicationName !== persisted.applicationName ||
    current.browserTitle !== persisted.browserTitle ||
    current.forgotPasswordUrl !== (persisted.forgotPasswordUrl ?? "") ||
    current.footerText !== persisted.footerText.trim()
  );
}
