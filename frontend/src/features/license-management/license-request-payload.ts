import type { AdUserListItem } from "../ad-management/types.ts";
import type {
  LicenseRequestAdUserSnapshot,
  LicenseRequestDetail,
  LicenseRequestFormRequest,
  LicenseRequestItemInput,
  LicenseRequestItemUserInput,
  LicenseRequestItemUserStatus,
  LicenseRequestOuSnapshot,
} from "./types.ts";
import { buildLicenseRequestPayloadBySource } from "./request-source-fields.ts";

export type LicenseRequestItemDraft = {
  clientId: string;
  productId: string;
  licenseType: LicenseRequestItemInput["licenseType"];
  requestedQuantity: string;
  justification: string;
  estimatedUnitCost: string;
  currency: string;
  vatIncluded: boolean;
  status: LicenseRequestItemInput["status"];
  users: (LicenseRequestAdUserSnapshot & { status?: LicenseRequestItemUserStatus })[];
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
    licenseType: "NamedUser",
    requestedQuantity: "1",
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
    licenseType: item.licenseType,
    requestedQuantity: String(item.requestedQuantity),
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
      status: user.status,
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
  requestSource: LicenseRequestFormRequest["requestSource"];
  requestDate: string;
  externalRequestNumber: string;
  ebysNumber: string;
  ebysDate: string | null;
  requesterUnit: LicenseRequestOuSnapshot;
  requesterManagerName: string;
  description: string;
  estimatedTotalCost: string;
  currency: string;
  vatIncluded: boolean;
  costNote: string;
  items: LicenseRequestItemDraft[];
}): LicenseRequestFormRequest {
  const itemPayloads: LicenseRequestItemInput[] = input.items.map((item) => ({
    productId: item.productId,
    licenseType: item.licenseType,
    requestedQuantity: item.licenseType === "NamedUser"
      ? item.users.length
      : Math.floor(Number(item.requestedQuantity)),
    estimatedUnitCost: parseOptionalDecimal(item.estimatedUnitCost),
    currency: item.currency.trim() || null,
    vatIncluded: item.vatIncluded,
    justification: item.justification.trim() || null,
    status: item.status,
    users: (item.licenseType === "NamedUser" ? item.users : []).map(
      (user): LicenseRequestItemUserInput => ({
        ...user,
        status: user.status ?? "Pending",
      }),
    ),
  }));

  const computedTotal = itemPayloads.reduce((sum, item) => {
    if (item.estimatedUnitCost == null) {
      return sum;
    }

    return sum + item.estimatedUnitCost * item.requestedQuantity;
  }, 0);

  const manualTotal = parseOptionalDecimal(input.estimatedTotalCost);

  return buildLicenseRequestPayloadBySource({
    requestSource: input.requestSource,
    requestDate: input.requestDate,
    externalRequestNumber: input.externalRequestNumber,
    ebysNumber: input.ebysNumber,
    ebysDate: input.ebysDate,
    requesterUnit: input.requesterUnit,
    requesterManagerName: input.requesterManagerName,
    description: input.description,
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

    const quantity = item.licenseType === "NamedUser"
      ? item.users.length
      : Math.floor(Number(item.requestedQuantity));
    return Number.isFinite(quantity) && quantity > 0 ? sum + unitCost * quantity : sum;
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
