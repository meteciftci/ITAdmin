import type { TFunction } from "i18next";

import type { AdDeletedObjectRestoreReadinessTextParams } from "@/features/ad-management/types";

function toI18nKey(key: string): string {
  return key.startsWith("adManagement:") ? key : `adManagement:${key}`;
}

export function translateReadinessText(
  t: TFunction,
  key: string | null | undefined,
  params?: AdDeletedObjectRestoreReadinessTextParams | null,
  legacyFallback?: string | null,
): string | null {
  const resolvedKey = key?.trim() || legacyFallback?.trim();
  if (!resolvedKey) {
    return null;
  }

  if (params && Object.keys(params).length > 0) {
    return t(toI18nKey(resolvedKey), params);
  }

  return t(toI18nKey(resolvedKey));
}
