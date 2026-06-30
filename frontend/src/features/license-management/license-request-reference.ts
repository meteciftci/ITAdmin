import type { TFunction } from "i18next";

import type { LicenseRequestSource } from "@/features/license-management/types";

type LicenseRequestReferenceInput = {
  requestSource: LicenseRequestSource;
  externalRequestNumber?: string | null;
  ebysNumber?: string | null;
};

export function formatLicenseRequestReference(
  request: LicenseRequestReferenceInput,
  t: TFunction<["licenseManagement"]>,
): string {
  switch (request.requestSource) {
    case "OfficialLetter": {
      const ebysNumber = request.ebysNumber?.trim();
      if (!ebysNumber) {
        return "-";
      }

      return t("licenseManagement:requests.reference.ebys", { value: ebysNumber });
    }
    case "CorporateRequestSystem": {
      const externalNumber = request.externalRequestNumber?.trim();
      if (!externalNumber) {
        return "-";
      }

      return t("licenseManagement:requests.reference.external", { value: externalNumber });
    }
    case "Email":
      return t("licenseManagement:requests.reference.email");
    case "VerbalInstruction":
      return t("licenseManagement:requests.reference.verbalInstruction");
    case "Other":
      return t("licenseManagement:requests.reference.other");
    default:
      return "-";
  }
}

export function shouldShowSourceDetailFieldsOnDetail(
  requestSource: LicenseRequestSource,
): boolean {
  return requestSource !== "OfficialLetter" && requestSource !== "CorporateRequestSystem";
}
