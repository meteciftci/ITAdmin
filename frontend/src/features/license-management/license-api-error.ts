import type { TFunction } from "i18next";

import { getApiErrorMessage } from "@/lib/api-error";

import { resolveLicenseManagementFieldLabel } from "./api-field-labels";

export function getLicenseManagementApiErrorMessage(
  error: unknown,
  t: TFunction,
  fallbackKey: string,
): string {
  return getApiErrorMessage(error, t(fallbackKey), {
    genericValidationMessage: t("errors:api.validation.fieldsCheck"),
    fieldLabelResolver: (fieldPath) => resolveLicenseManagementFieldLabel(t, fieldPath),
  });
}
