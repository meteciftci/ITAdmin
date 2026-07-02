import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildLicenseRequestPayloadBySource,
  isRequestSourceFieldVisible,
} from "./request-source-fields.ts";
import type { LicenseRequestOuSnapshot } from "./types.ts";

const unit: LicenseRequestOuSnapshot = {
  objectGuid: "ou-guid",
  displayName: "IT",
  distinguishedName: "OU=IT,DC=test",
};

describe("isRequestSourceFieldVisible", () => {
  it("shows the external request number only for CorporateRequestSystem", () => {
    assert.equal(isRequestSourceFieldVisible("externalRequestNumber", "CorporateRequestSystem"), true);
    assert.equal(isRequestSourceFieldVisible("externalRequestNumber", "OfficialLetter"), false);
    assert.equal(isRequestSourceFieldVisible("externalRequestNumber", "Email"), false);
  });

  it("shows the EBYS fields only for OfficialLetter", () => {
    for (const field of ["ebysNumber", "ebysDate"] as const) {
      assert.equal(isRequestSourceFieldVisible(field, "OfficialLetter"), true);
      assert.equal(isRequestSourceFieldVisible(field, "CorporateRequestSystem"), false);
      assert.equal(isRequestSourceFieldVisible(field, "Email"), false);
    }
  });

  it("shows the description for free-form sources only", () => {
    assert.equal(isRequestSourceFieldVisible("description", "Email"), true);
    assert.equal(isRequestSourceFieldVisible("description", "VerbalInstruction"), true);
    assert.equal(isRequestSourceFieldVisible("description", "Other"), true);
    assert.equal(isRequestSourceFieldVisible("description", "OfficialLetter"), false);
    assert.equal(isRequestSourceFieldVisible("description", "CorporateRequestSystem"), false);
  });

  it("always shows the requester manager name", () => {
    assert.equal(isRequestSourceFieldVisible("requesterManagerName", "Email"), true);
    assert.equal(isRequestSourceFieldVisible("requesterManagerName", "OfficialLetter"), true);
  });
});

describe("buildLicenseRequestPayloadBySource", () => {
  const base = {
    requestDate: "2026-07-01",
    externalRequestNumber: "EXT-1",
    ebysNumber: "EBYS-1",
    ebysDate: "2026-07-01",
    requesterUnit: unit,
    requesterManagerName: " Manager ",
    description: " note ",
    status: "Pending" as const,
    estimatedTotalCost: null,
    currency: null,
    vatIncluded: false,
    costNote: null,
    items: [],
  };

  it("keeps only the external number for CorporateRequestSystem", () => {
    const payload = buildLicenseRequestPayloadBySource({
      ...base,
      requestSource: "CorporateRequestSystem",
    });
    assert.equal(payload.externalRequestNumber, "EXT-1");
    assert.equal(payload.ebysNumber, null);
    assert.equal(payload.ebysDate, null);
  });

  it("keeps only the EBYS fields for OfficialLetter", () => {
    const payload = buildLicenseRequestPayloadBySource({
      ...base,
      requestSource: "OfficialLetter",
    });
    assert.equal(payload.externalRequestNumber, null);
    assert.equal(payload.ebysNumber, "EBYS-1");
    assert.equal(payload.ebysDate, "2026-07-01");
  });

  it("clears both external and EBYS fields for Email and trims free text", () => {
    const payload = buildLicenseRequestPayloadBySource({ ...base, requestSource: "Email" });
    assert.equal(payload.externalRequestNumber, null);
    assert.equal(payload.ebysNumber, null);
    assert.equal(payload.ebysDate, null);
    assert.equal(payload.requesterManagerName, "Manager");
    assert.equal(payload.description, "note");
  });

  it("converts blank optional text to null", () => {
    const payload = buildLicenseRequestPayloadBySource({
      ...base,
      requestSource: "Email",
      requesterManagerName: "   ",
      description: "",
    });
    assert.equal(payload.requesterManagerName, null);
    assert.equal(payload.description, null);
  });
});
