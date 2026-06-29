import type {
  LicensePurchaseFormRequest,
  LicensePurchaseStatus,
  LicensePurchaseType,
} from "@/features/license-management/types";

export type PurchaseFormFieldKey =
  | "tenderNumber"
  | "tenderDate"
  | "directPurchaseNumber"
  | "dmoOrderNumber"
  | "contractNumber"
  | "contractStartDate"
  | "contractEndDate"
  | "ebysNumber"
  | "ebysDate"
  | "invoiceNumber"
  | "invoiceDate"
  | "supplierCompanyId"
  | "supportCompanyId"
  | "actualTotalCost"
  | "currency"
  | "vatIncluded";

export type PurchaseFormSectionKey =
  | "basic"
  | "purchaseInfo"
  | "officialDocuments"
  | "companyAndCost"
  | "notes";

export type PurchaseFormRawValues = {
  purchaseType: LicensePurchaseType;
  title: string;
  description: string;
  purchaseDate: string | null;
  tenderNumber: string;
  tenderDate: string | null;
  directPurchaseNumber: string;
  dmoOrderNumber: string;
  ebysNumber: string;
  ebysDate: string | null;
  invoiceNumber: string;
  invoiceDate: string | null;
  contractNumber: string;
  contractStartDate: string | null;
  contractEndDate: string | null;
  supplierCompanyId: string;
  supportCompanyId: string;
  actualTotalCost: string;
  currency: string;
  vatIncluded: boolean;
  notes: string;
  status: LicensePurchaseStatus;
};

const OFFICIAL_DOCUMENT_FIELDS: readonly PurchaseFormFieldKey[] = [
  "tenderNumber",
  "tenderDate",
  "directPurchaseNumber",
  "dmoOrderNumber",
  "contractNumber",
  "contractStartDate",
  "contractEndDate",
  "ebysNumber",
  "ebysDate",
  "invoiceNumber",
  "invoiceDate",
];

const TYPE_VISIBLE_FIELDS: Record<LicensePurchaseType, ReadonlySet<PurchaseFormFieldKey>> = {
  Tender: new Set([
    "tenderNumber",
    "tenderDate",
    "contractNumber",
    "contractStartDate",
    "contractEndDate",
    "ebysNumber",
    "ebysDate",
    "invoiceNumber",
    "invoiceDate",
    "supplierCompanyId",
    "supportCompanyId",
    "actualTotalCost",
    "currency",
    "vatIncluded",
  ]),
  DirectPurchase: new Set([
    "directPurchaseNumber",
    "ebysNumber",
    "ebysDate",
    "invoiceNumber",
    "invoiceDate",
    "supplierCompanyId",
    "supportCompanyId",
    "actualTotalCost",
    "currency",
    "vatIncluded",
  ]),
  Dmo: new Set([
    "dmoOrderNumber",
    "ebysNumber",
    "ebysDate",
    "invoiceNumber",
    "invoiceDate",
    "supplierCompanyId",
    "supportCompanyId",
    "actualTotalCost",
    "currency",
    "vatIncluded",
  ]),
  Renewal: new Set([
    "contractNumber",
    "contractStartDate",
    "contractEndDate",
    "ebysNumber",
    "ebysDate",
    "invoiceNumber",
    "invoiceDate",
    "supplierCompanyId",
    "supportCompanyId",
    "actualTotalCost",
    "currency",
    "vatIncluded",
  ]),
  CorporateSubscription: new Set([
    "contractNumber",
    "contractStartDate",
    "contractEndDate",
    "ebysNumber",
    "ebysDate",
    "invoiceNumber",
    "invoiceDate",
    "supplierCompanyId",
    "supportCompanyId",
    "actualTotalCost",
    "currency",
    "vatIncluded",
  ]),
  LegacyPerpetual: new Set([
    "supplierCompanyId",
    "supportCompanyId",
    "actualTotalCost",
    "currency",
    "vatIncluded",
  ]),
  Other: new Set([
    "supplierCompanyId",
    "supportCompanyId",
    "actualTotalCost",
    "currency",
    "vatIncluded",
  ]),
};

export function isPurchaseFieldVisible(
  field: PurchaseFormFieldKey,
  purchaseType: LicensePurchaseType,
): boolean {
  return TYPE_VISIBLE_FIELDS[purchaseType].has(field);
}

export function isPurchaseSectionVisible(
  section: PurchaseFormSectionKey,
  purchaseType: LicensePurchaseType,
): boolean {
  switch (section) {
    case "basic":
      return true;
    case "purchaseInfo":
      return true;
    case "officialDocuments":
      return OFFICIAL_DOCUMENT_FIELDS.some((field) => isPurchaseFieldVisible(field, purchaseType));
    case "companyAndCost":
      return (
        isPurchaseFieldVisible("supplierCompanyId", purchaseType)
        || isPurchaseFieldVisible("supportCompanyId", purchaseType)
        || isPurchaseFieldVisible("actualTotalCost", purchaseType)
        || isPurchaseFieldVisible("currency", purchaseType)
        || isPurchaseFieldVisible("vatIncluded", purchaseType)
      );
    case "notes":
      return true;
    default:
      return false;
  }
}

function emptyToNull(value: string): string | null {
  return value.trim() ? value.trim() : null;
}

export function buildPurchasePayloadByType(
  values: PurchaseFormRawValues,
): LicensePurchaseFormRequest & { status: LicensePurchaseStatus } {
  const visible = TYPE_VISIBLE_FIELDS[values.purchaseType];

  return {
    purchaseType: values.purchaseType,
    title: values.title.trim(),
    description: emptyToNull(values.description),
    purchaseDate: values.purchaseDate,
    tenderNumber: visible.has("tenderNumber") ? emptyToNull(values.tenderNumber) : null,
    tenderDate: visible.has("tenderDate") ? values.tenderDate : null,
    directPurchaseNumber: visible.has("directPurchaseNumber")
      ? emptyToNull(values.directPurchaseNumber)
      : null,
    dmoOrderNumber: visible.has("dmoOrderNumber") ? emptyToNull(values.dmoOrderNumber) : null,
    ebysNumber: visible.has("ebysNumber") ? emptyToNull(values.ebysNumber) : null,
    ebysDate: visible.has("ebysDate") ? values.ebysDate : null,
    invoiceNumber: visible.has("invoiceNumber") ? emptyToNull(values.invoiceNumber) : null,
    invoiceDate: visible.has("invoiceDate") ? values.invoiceDate : null,
    contractNumber: visible.has("contractNumber") ? emptyToNull(values.contractNumber) : null,
    contractStartDate: visible.has("contractStartDate") ? values.contractStartDate : null,
    contractEndDate: visible.has("contractEndDate") ? values.contractEndDate : null,
    supplierCompanyId: visible.has("supplierCompanyId")
      ? emptyToNull(values.supplierCompanyId)
      : null,
    supportCompanyId: visible.has("supportCompanyId")
      ? emptyToNull(values.supportCompanyId)
      : null,
    actualTotalCost:
      visible.has("actualTotalCost") && values.actualTotalCost
        ? Number(values.actualTotalCost)
        : null,
    currency: visible.has("currency") ? emptyToNull(values.currency) : null,
    vatIncluded: visible.has("vatIncluded") ? values.vatIncluded : null,
    notes: emptyToNull(values.notes),
    status: values.status,
  };
}
