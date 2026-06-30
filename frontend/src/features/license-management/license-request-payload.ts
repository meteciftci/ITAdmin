import type { AdUserListItem } from "@/features/ad-management/types";
import type {
  LicenseRequestAdUserSnapshot,
  LicenseRequestDetail,
  LicenseRequestFormRequest,
  LicenseRequestItemInput,
  LicenseRequestItemUserInput,
  LicenseRequestOuSnapshot,
} from "@/features/license-management/types";
import { buildLicenseRequestPayloadBySource } from "@/features/license-management/request-source-fields";

export type LicenseRequestItemDraft = {
  clientId: string;
  productId: string;
  justification: string;
  estimatedUnitCost: string;
  currency: string;
  vatIncluded: boolean;
  status: LicenseRequestItemInput["status"];
  users: LicenseRequestAdUserSnapshot[];
};

export function mapAdUserToSnapshot(user: AdUserListItem): LicenseRequestAdUserSnapshot {
  return {
    adObjectId: user.id,
    samAccountName: user.samAccountName,
    userPrincipalName: user.userPrincipalName,
    displayName: user.displayName,
    department: user.department,
    title: null,
    mail: user.mail,
    phone: null,
  };
}

export function createEmptyRequestItemDraft(currency = "TRY"): LicenseRequestItemDraft {
  return {
    clientId: crypto.randomUUID(),
    productId: "",
    justification: "",
    estimatedUnitCost: "",
    currency,
    vatIncluded: false,
    status: "Pending",
    users: [],
  };
}

export function mapDetailToItemDrafts(request: LicenseRequestDetail): LicenseRequestItemDraft[] {
  return request.items.map((item) => ({
    clientId: item.id,
    productId: item.productId,
    justification: item.justification ?? "",
    estimatedUnitCost: item.estimatedUnitCost?.toString() ?? "",
    currency: item.currency ?? request.currency ?? "TRY",
    vatIncluded: item.vatIncluded ?? false,
    status: item.status,
    users: item.users.map((user) => ({
      adObjectId: user.adObjectId,
      samAccountName: user.samAccountName,
      userPrincipalName: user.userPrincipalName,
      displayName: user.displayName,
      department: user.department,
      title: user.title,
      mail: user.mail,
      phone: user.phone,
    })),
  }));
}

function parseOptionalDecimal(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  const parsed = Number(trimmed.replace(",", "."));
  return Number.isFinite(parsed) ? parsed : null;
}

export function buildLicenseRequestPayload(input: {
  requestNumber: string;
  requestSource: LicenseRequestFormRequest["requestSource"];
  requestDate: string;
  externalRequestNumber: string;
  ebysNumber: string;
  ebysDate: string | null;
  requesterUnit: LicenseRequestOuSnapshot;
  requesterManagerName: string;
  description: string;
  status: LicenseRequestFormRequest["status"];
  estimatedTotalCost: string;
  currency: string;
  vatIncluded: boolean;
  costNote: string;
  items: LicenseRequestItemDraft[];
}): LicenseRequestFormRequest {
  const itemPayloads: LicenseRequestItemInput[] = input.items.map((item) => ({
    productId: item.productId,
    estimatedUnitCost: parseOptionalDecimal(item.estimatedUnitCost),
    currency: item.currency.trim() || null,
    vatIncluded: item.vatIncluded,
    justification: item.justification.trim() || null,
    status: item.status,
    users: item.users.map(
      (user): LicenseRequestItemUserInput => ({
        ...user,
        status: "Pending",
      }),
    ),
  }));

  const computedTotal = itemPayloads.reduce((sum, item) => {
    if (item.estimatedUnitCost == null) {
      return sum;
    }

    return sum + item.estimatedUnitCost * item.users.length;
  }, 0);

  const manualTotal = parseOptionalDecimal(input.estimatedTotalCost);

  return buildLicenseRequestPayloadBySource({
    requestNumber: input.requestNumber.trim(),
    requestSource: input.requestSource,
    requestDate: input.requestDate,
    externalRequestNumber: input.externalRequestNumber,
    ebysNumber: input.ebysNumber,
    ebysDate: input.ebysDate,
    requesterUnit: input.requesterUnit,
    requesterManagerName: input.requesterManagerName,
    description: input.description,
    status: input.status,
    estimatedTotalCost: manualTotal ?? (computedTotal > 0 ? computedTotal : null),
    currency: input.currency.trim() || null,
    vatIncluded: input.vatIncluded,
    costNote: input.costNote.trim() || null,
    items: itemPayloads,
  });
}

export function calculateItemsEstimatedTotal(items: LicenseRequestItemDraft[]): number {
  return items.reduce((sum, item) => {
    const unitCost = parseOptionalDecimal(item.estimatedUnitCost);
    if (unitCost == null) {
      return sum;
    }

    return sum + unitCost * item.users.length;
  }, 0);
}

export function formatRequestUserCountLabel(
  t: (key: string, options?: { count: number }) => string,
  count: number,
): string {
  const key = count === 1
    ? "licenseManagement:requests.fields.userCountSingular"
    : "licenseManagement:requests.fields.userCountPlural";
  return t(key, { count });
}
