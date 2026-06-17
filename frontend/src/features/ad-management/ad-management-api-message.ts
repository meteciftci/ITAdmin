import { AxiosError } from "axios";
import type { TFunction } from "i18next";

import type { AdManagementApiMessageFields } from "@/features/ad-management/types";

function toI18nKey(messageKey: string): string {
  return messageKey.startsWith("adManagement:") ? messageKey : `adManagement:${messageKey}`;
}

export function resolveAdManagementApiMessage(
  t: TFunction,
  source: AdManagementApiMessageFields | null | undefined,
  fallbackKey: string,
): string {
  if (source?.messageKey?.trim()) {
    const key = toI18nKey(source.messageKey.trim());
    if (source.messageParams && Object.keys(source.messageParams).length > 0) {
      return t(key, source.messageParams);
    }
    return t(key);
  }

  if (source?.message?.trim()) {
    return source.message.trim();
  }

  return t(fallbackKey);
}

export function getAdManagementApiErrorMessage(
  error: unknown,
  t: TFunction,
  fallbackKey: string,
): string {
  if (error instanceof AxiosError) {
    const data = error.response?.data;
    if (data && typeof data === "object") {
      const resolved = resolveAdManagementApiMessage(
        t,
        data as AdManagementApiMessageFields,
        fallbackKey,
      );
      const fields = data as AdManagementApiMessageFields;
      if (fields.messageKey?.trim() || fields.message?.trim()) {
        return resolved;
      }

      const validation = (data as { validation?: AdManagementApiMessageFields }).validation;
      if (validation) {
        return resolveAdManagementApiMessage(t, validation, fallbackKey);
      }
    }

    if (typeof data === "string" && data.trim()) {
      return data.trim();
    }
  }

  return t(fallbackKey);
}
