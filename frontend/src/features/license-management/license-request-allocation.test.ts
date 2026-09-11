import assert from "node:assert/strict";
import { describe, it } from "node:test";
import type { TFunction } from "i18next";

import { validateLicenseRequestForm } from "./license-request-form-validation.ts";
import {
  buildLicenseRequestPayload,
  calculateItemsEstimatedTotal,
  type LicenseRequestItemDraft,
} from "./license-request-payload.ts";

const t = ((key: string) => key) as TFunction<["licenseManagement", "common"]>;

function item(overrides: Partial<LicenseRequestItemDraft> = {}): LicenseRequestItemDraft {
  return {
    clientId: "item-1",
    productId: "product-1",
    licenseType: "Concurrent",
    requestedQuantity: "10",
    justification: "Pool",
    estimatedUnitCost: "25",
    currency: "TRY",
    vatIncluded: false,
    status: "Pending",
    users: [],
    ...overrides,
  };
}

function build(items: LicenseRequestItemDraft[]) {
  return buildLicenseRequestPayload({
    requestSource: "Email",
    requestDate: "2026-09-10",
    externalRequestNumber: "",
    ebysNumber: "",
    ebysDate: null,
    requesterUnit: {
      objectGuid: "ou-1",
      displayName: "IT",
      distinguishedName: "OU=IT,DC=test",
    },
    requesterManagerName: "",
    description: "",
    estimatedTotalCost: "",
    currency: "TRY",
    vatIncluded: false,
    costNote: "",
    items,
  });
}

describe("license request allocation model", () => {
  it("builds a quantity-based request without named users", () => {
    const payload = build([item({ requestedQuantity: "12" })]);

    assert.equal(payload.items[0]?.licenseType, "Concurrent");
    assert.equal(payload.items[0]?.requestedQuantity, 12);
    assert.deepEqual(payload.items[0]?.users, []);
    assert.equal(payload.items[0]?.estimatedTotalCost, undefined);
    assert.equal(payload.estimatedTotalCost, 300);
    assert.equal(calculateItemsEstimatedTotal([item({ requestedQuantity: "12" })]), 300);
  });

  it("derives named-user quantity from the selected users", () => {
    const named = item({
      licenseType: "NamedUser",
      requestedQuantity: "99",
      users: [
        { adObjectId: "u1", displayName: "Ada", status: "Approved" },
        { adObjectId: "u2", displayName: "Grace" },
      ],
    });
    const payload = build([named]);

    assert.equal(payload.items[0]?.requestedQuantity, 2);
    assert.deepEqual(payload.items[0]?.users.map((x) => x.status), ["Approved", "Pending"]);
  });

  it("accepts positive quantity without users and rejects invalid quantity", () => {
    const base = {
      requestDate: "2026-09-10",
      requestSource: "Email" as const,
      requesterUnit: {
        objectGuid: "ou-1",
        displayName: "IT",
        distinguishedName: "OU=IT,DC=test",
      },
      externalRequestNumber: "",
      ebysNumber: "",
      ebysDate: null,
    };

    assert.deepEqual(validateLicenseRequestForm(t, { ...base, items: [item()] }), { isValid: true });
    const invalid = validateLicenseRequestForm(t, {
      ...base,
      items: [item({ requestedQuantity: "0" })],
    });
    assert.equal(invalid.isValid, false);
    assert.match((invalid as { message: string }).message, /quantityRequired/);
  });
});
