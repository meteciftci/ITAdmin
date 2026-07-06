import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildConvertPayload,
  clampFulfillQuantity,
  distinctProductIds,
  summarizeByProduct,
  validateSelection,
  type FulfillmentSelectionLine,
} from "./license-fulfillment-conversion.ts";
import type { LicenseFulfillmentCandidate } from "./types.ts";

function candidate(overrides: Partial<LicenseFulfillmentCandidate> = {}): LicenseFulfillmentCandidate {
  return {
    requestId: "req-1",
    requestItemId: "item-1",
    requestSource: "Email",
    requestDate: "2026-07-01",
    requesterUnitDisplayName: "IT",
    productId: "p1",
    productName: "Photoshop",
    productBrand: "Adobe",
    requestedQuantity: 10,
    approvedQuantity: 10,
    fulfilledQuantity: 0,
    remainingQuantity: 10,
    itemStatus: "Approved",
    ...overrides,
  };
}

function line(c: Partial<LicenseFulfillmentCandidate>, fulfillQuantity: number): FulfillmentSelectionLine {
  return { candidate: candidate(c), fulfillQuantity };
}

describe("summarizeByProduct", () => {
  it("aggregates quantities per product across lines", () => {
    const summary = summarizeByProduct([
      line({ productId: "p1", requestItemId: "a" }, 3),
      line({ productId: "p1", requestItemId: "b" }, 4),
      line({ productId: "p2", productName: "OSKA", requestItemId: "c" }, 2),
    ]);

    assert.equal(summary.length, 2);
    const p1 = summary.find((x) => x.productId === "p1");
    assert.equal(p1?.lineCount, 2);
    assert.equal(p1?.totalQuantity, 7);
    assert.equal(summary.find((x) => x.productId === "p2")?.totalQuantity, 2);
  });
});

describe("clampFulfillQuantity", () => {
  it("clamps to [1, remaining] and floors fractions", () => {
    assert.equal(clampFulfillQuantity(5, 10), 5);
    assert.equal(clampFulfillQuantity(0, 10), 1);
    assert.equal(clampFulfillQuantity(-3, 10), 1);
    assert.equal(clampFulfillQuantity(20, 10), 10);
    assert.equal(clampFulfillQuantity(4.9, 10), 4);
    assert.equal(clampFulfillQuantity(Number.NaN, 10), 1);
  });
});

describe("validateSelection", () => {
  it("rejects an empty selection", () => {
    const result = validateSelection([]);
    assert.equal(result.isValid, false);
    assert.match((result as { messageKey: string }).messageKey, /noLines/);
  });

  it("rejects a quantity above the remaining", () => {
    const result = validateSelection([line({ remainingQuantity: 5 }, 6)]);
    assert.equal(result.isValid, false);
    assert.match((result as { messageKey: string }).messageKey, /quantityRange/);
  });

  it("accepts in-range quantities", () => {
    assert.deepEqual(validateSelection([line({ remainingQuantity: 5 }, 5)]), { isValid: true });
  });
});

describe("distinctProductIds", () => {
  it("returns unique product ids preserving first occurrence", () => {
    const ids = distinctProductIds([
      line({ productId: "p1", requestItemId: "a" }, 1),
      line({ productId: "p2", requestItemId: "b" }, 1),
      line({ productId: "p1", requestItemId: "c" }, 1),
    ]);
    assert.deepEqual(ids, ["p1", "p2"]);
  });
});

describe("buildConvertPayload", () => {
  const lines = [line({ productId: "p1", requestItemId: "a" }, 3)];
  const defaults = [
    { productId: "p1", licenseType: "Subscription" as const, startDate: null, endDate: null, isPerpetual: false },
  ];

  it("targets a new purchase", () => {
    const newPurchase = {
      purchaseType: "DirectPurchase" as const,
      title: "P",
      description: null,
      purchaseDate: null,
      supplierCompanyId: null,
      supportCompanyId: null,
      actualTotalCost: null,
      currency: "TRY",
      vatIncluded: false,
      notes: null,
    };
    const payload = buildConvertPayload(lines, { kind: "new", purchase: newPurchase }, defaults);
    assert.equal(payload.existingPurchaseId, null);
    assert.deepEqual(payload.newPurchase, newPurchase);
    assert.deepEqual(payload.lines, [{ requestItemId: "a", fulfillQuantity: 3 }]);
    assert.deepEqual(payload.packageDefaults, defaults);
  });

  it("targets an existing purchase", () => {
    const payload = buildConvertPayload(lines, { kind: "existing", purchaseId: "pur-1" }, defaults);
    assert.equal(payload.existingPurchaseId, "pur-1");
    assert.equal(payload.newPurchase, null);
  });
});
