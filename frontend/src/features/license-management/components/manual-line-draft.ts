import type { ConvertFulfillmentManualLine } from "@/features/license-management/types";

export type ManualLineDraft = ConvertFulfillmentManualLine & { key: string };

export function createManualLineDraft(): ManualLineDraft {
  return {
    key: crypto.randomUUID(),
    productId: "",
    quantity: 1,
    licenseType: "Subscription",
    startDate: null,
    endDate: null,
    isPerpetual: false,
  };
}
