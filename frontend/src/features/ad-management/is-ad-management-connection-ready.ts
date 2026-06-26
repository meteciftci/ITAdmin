import type { AdManagementSettings } from "@/features/ad-management/types";

const SUCCESS_VALIDATION_STATUS = "Ok";

export function isAdManagementConnectionReady(
  settings: AdManagementSettings | null | undefined,
): boolean {
  if (!settings?.isEnabled) {
    return false;
  }

  const hasCoreFields = Boolean(
    settings.domainFqdn?.trim()
      && settings.netbiosDomainName?.trim()
      && settings.defaultNamingContext?.trim()
      && settings.baseDn?.trim()
      && settings.serviceAccountUserName?.trim()
      && settings.hasServiceAccountPassword,
  );

  if (!hasCoreFields) {
    return false;
  }

  if (!settings.lastValidationStatus) {
    return false;
  }

  return settings.lastValidationStatus.trim().toLowerCase() === SUCCESS_VALIDATION_STATUS.toLowerCase();
}
