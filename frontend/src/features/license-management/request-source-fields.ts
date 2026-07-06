import type { LicenseRequestSource } from "@/features/license-management/types";

export type RequestSourceFieldName =
  | "externalRequestNumber"
  | "ebysNumber"
  | "ebysDate"
  | "description"
  | "requesterManagerName";

export function isRequestSourceFieldVisible(
  field: RequestSourceFieldName,
  source: LicenseRequestSource,
): boolean {
  switch (field) {
    case "externalRequestNumber":
      return source === "CorporateRequestSystem";
    case "ebysNumber":
    case "ebysDate":
      return source === "OfficialLetter";
    case "description":
      return (
        source === "Email"
        || source === "VerbalInstruction"
        || source === "Other"
      );
    case "requesterManagerName":
      return true;
    default:
      return false;
  }
}

export function buildLicenseRequestPayloadBySource(input: {
  requestSource: LicenseRequestSource;
  requestDate: string;
  externalRequestNumber: string;
  ebysNumber: string;
  ebysDate: string | null;
  requesterUnit: {
    objectGuid: string;
    displayName: string;
    distinguishedName: string;
  };
  requesterManagerName: string;
  description: string;
  estimatedTotalCost: number | null;
  currency: string | null;
  vatIncluded: boolean;
  costNote: string | null;
  items: import("@/features/license-management/types").LicenseRequestItemInput[];
}) {
  const showExternal = isRequestSourceFieldVisible("externalRequestNumber", input.requestSource);
  const showEbys = isRequestSourceFieldVisible("ebysNumber", input.requestSource);

  return {
    requestSource: input.requestSource,
    requestDate: input.requestDate,
    externalRequestNumber: showExternal ? input.externalRequestNumber.trim() || null : null,
    ebysNumber: showEbys ? input.ebysNumber.trim() || null : null,
    ebysDate: showEbys ? input.ebysDate : null,
    requesterUnit: input.requesterUnit,
    requesterManagerName: input.requesterManagerName.trim() || null,
    description: input.description.trim() || null,
    estimatedTotalCost: input.estimatedTotalCost,
    currency: input.currency,
    vatIncluded: input.vatIncluded,
    costNote: input.costNote,
    items: input.items,
  };
}
