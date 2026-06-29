import assert from "node:assert/strict";
import { describe, it, test } from "node:test";

import {
  validateCompanyForm,
  validatePackageForm,
  validateProductForm,
  validatePurchaseForm,
} from "./form-validation.ts";
import { buildLicensedProductPayload } from "./product-form-payload.ts";
import { formatLicensedProductLabel } from "./product-labels.ts";
import {
  buildPurchasePayloadByType,
  isPurchaseFieldVisible,
  type PurchaseFormRawValues,
} from "./purchase-form-fields.ts";

test("validateCompanyForm requires name", () => {
  assert.equal(validateCompanyForm("", "", "", ""), "nameRequired");
});

test("validateProductForm requires name and category", () => {
  assert.equal(validateProductForm("", ""), "nameRequired");
  assert.equal(validateProductForm("Photoshop", ""), "categoryRequired");
  assert.equal(validateProductForm("Photoshop", "category-id"), null);
});

test("validatePurchaseForm requires title", () => {
  assert.equal(validatePurchaseForm(""), "titleRequired");
});

test("validatePackageForm enforces quantity minimum", () => {
  assert.equal(validatePackageForm("a", "b", 0), "quantityMin");
  assert.equal(validatePackageForm("", "b", 1), "purchaseRequired");
  assert.equal(validatePackageForm("a", "", 1), "productRequired");
  assert.equal(validatePackageForm("a", "b", 5), null);
});

describe("buildLicensedProductPayload", () => {
  it("sends brand and categoryId", () => {
    const payload = buildLicensedProductPayload({
      name: "Photoshop",
      brand: "Adobe",
      categoryId: "category-id",
      description: "",
      isActive: true,
    });

    assert.equal(payload.brand, "Adobe");
    assert.equal(payload.categoryId, "category-id");
    assert.equal(payload.description, null);
  });

  it("sends null brand when empty", () => {
    const payload = buildLicensedProductPayload({
      name: "Photoshop",
      brand: "",
      categoryId: "category-id",
      description: "Design tool",
      isActive: true,
    });

    assert.equal(payload.brand, null);
    assert.equal(payload.description, "Design tool");
  });
});

describe("formatLicensedProductLabel", () => {
  it("joins name, brand and category with em dash", () => {
    const label = formatLicensedProductLabel({
      name: "Photoshop",
      brand: "Adobe",
      categoryName: "Design",
    });

    assert.equal(label, "Photoshop — Adobe — Design");
  });

  it("omits empty brand and category parts", () => {
    const label = formatLicensedProductLabel({
      name: "Photoshop",
      brand: null,
      categoryName: "Design",
    });

    assert.equal(label, "Photoshop — Design");
  });
});

function createPurchaseValues(
  overrides: Partial<PurchaseFormRawValues> = {},
): PurchaseFormRawValues {
  return {
    purchaseType: "DirectPurchase",
    title: "Test purchase",
    description: "",
    purchaseDate: null,
    tenderNumber: "T-1",
    tenderDate: "2026-01-01",
    directPurchaseNumber: "DP-1",
    dmoOrderNumber: "DMO-1",
    contractNumber: "C-1",
    contractStartDate: "2026-01-01",
    contractEndDate: "2026-12-31",
    ebysNumber: "E-1",
    ebysDate: "2026-02-01",
    invoiceNumber: "INV-1",
    invoiceDate: "2026-03-01",
    supplierCompanyId: "",
    supportCompanyId: "",
    actualTotalCost: "",
    currency: "",
    vatIncluded: false,
    notes: "",
    status: "Draft",
    ...overrides,
  };
}

describe("purchase form field visibility", () => {
  it("shows tender fields only for Tender", () => {
    assert.equal(isPurchaseFieldVisible("tenderNumber", "Tender"), true);
    assert.equal(isPurchaseFieldVisible("tenderNumber", "DirectPurchase"), false);
    assert.equal(isPurchaseFieldVisible("directPurchaseNumber", "DirectPurchase"), true);
    assert.equal(isPurchaseFieldVisible("dmoOrderNumber", "Dmo"), true);
    assert.equal(isPurchaseFieldVisible("dmoOrderNumber", "Tender"), false);
  });

  it("hides official document fields for LegacyPerpetual", () => {
    assert.equal(isPurchaseFieldVisible("tenderNumber", "LegacyPerpetual"), false);
    assert.equal(isPurchaseFieldVisible("ebysNumber", "LegacyPerpetual"), false);
    assert.equal(isPurchaseFieldVisible("invoiceNumber", "LegacyPerpetual"), false);
    assert.equal(isPurchaseFieldVisible("contractNumber", "LegacyPerpetual"), false);
  });
});

describe("buildPurchasePayloadByType", () => {
  it("clears tender fields for DirectPurchase", () => {
    const payload = buildPurchasePayloadByType(
      createPurchaseValues({ purchaseType: "DirectPurchase" }),
    );

    assert.equal(payload.tenderNumber, null);
    assert.equal(payload.tenderDate, null);
    assert.equal(payload.dmoOrderNumber, null);
    assert.equal(payload.contractStartDate, null);
    assert.equal(payload.contractEndDate, null);
    assert.equal(payload.directPurchaseNumber, "DP-1");
  });

  it("clears direct purchase fields for Tender", () => {
    const payload = buildPurchasePayloadByType(createPurchaseValues({ purchaseType: "Tender" }));

    assert.equal(payload.directPurchaseNumber, null);
    assert.equal(payload.dmoOrderNumber, null);
    assert.equal(payload.tenderNumber, "T-1");
  });

  it("clears unrelated fields when purchase type changes from Tender to DirectPurchase", () => {
    const payload = buildPurchasePayloadByType(
      createPurchaseValues({
        purchaseType: "DirectPurchase",
        tenderNumber: "leftover",
        tenderDate: "2026-05-01",
      }),
    );

    assert.equal(payload.tenderNumber, null);
    assert.equal(payload.tenderDate, null);
  });

  it("clears official document fields for LegacyPerpetual", () => {
    const payload = buildPurchasePayloadByType(
      createPurchaseValues({ purchaseType: "LegacyPerpetual" }),
    );

    assert.equal(payload.tenderNumber, null);
    assert.equal(payload.directPurchaseNumber, null);
    assert.equal(payload.dmoOrderNumber, null);
    assert.equal(payload.ebysNumber, null);
    assert.equal(payload.invoiceNumber, null);
    assert.equal(payload.contractNumber, null);
  });
});
