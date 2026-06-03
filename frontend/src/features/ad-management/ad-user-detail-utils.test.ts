import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildAdUserGroupsSummary,
  filterMappedAttributesForDisplay,
  formatMappedAdUserAttributeValue,
  hasMappedAttributeValue,
  isGuidLike,
} from "./ad-user-detail-utils.ts";
import type { MappedAdUserAttribute } from "./types.ts";

function createMappedAttribute(
  overrides: Partial<MappedAdUserAttribute> = {},
): MappedAdUserAttribute {
  return {
    logicalField: "fieldA",
    displayName: "Field A",
    adAttribute: "extensionAttribute1",
    value: null,
    isSensitive: false,
    maskingStrategy: null,
    isEditable: true,
    isSearchable: false,
    sortOrder: 1,
    ...overrides,
  };
}

describe("filterMappedAttributesForDisplay", () => {
  it("returns only populated mapped attributes by default", () => {
    const attributes = [
      createMappedAttribute({ logicalField: "a", sortOrder: 2, value: null }),
      createMappedAttribute({ logicalField: "b", sortOrder: 1, value: ["x"] }),
      createMappedAttribute({ logicalField: "c", sortOrder: 3, value: ["  "] }),
    ];

    const result = filterMappedAttributesForDisplay(attributes, false);
    assert.equal(result.length, 1);
    assert.equal(result[0]?.logicalField, "b");
  });

  it("includes empty mapped attributes when showEmptyFields is enabled", () => {
    const attributes = [
      createMappedAttribute({ logicalField: "a", sortOrder: 2, value: null }),
      createMappedAttribute({ logicalField: "b", sortOrder: 1, value: ["x"] }),
    ];

    const result = filterMappedAttributesForDisplay(attributes, true);
    assert.equal(result.length, 2);
    assert.deepEqual(
      result.map((item) => item.logicalField),
      ["b", "a"],
    );
  });
});

describe("formatMappedAdUserAttributeValue", () => {
  it("returns dash for empty values", () => {
    assert.equal(formatMappedAdUserAttributeValue(createMappedAttribute()), "-");
  });

  it("joins multiple values", () => {
    const value = formatMappedAdUserAttributeValue(
      createMappedAttribute({ value: ["a", "b"] }),
    );
    assert.equal(value, "a, b");
  });
});

describe("hasMappedAttributeValue", () => {
  it("detects non-empty trimmed values", () => {
    assert.equal(hasMappedAttributeValue(createMappedAttribute({ value: ["x"] })), true);
    assert.equal(hasMappedAttributeValue(createMappedAttribute({ value: ["  "] })), false);
  });
});

describe("buildAdUserGroupsSummary", () => {
  it("returns preview groups and remaining count", () => {
    const groups = Array.from({ length: 12 }, (_, index) => ({
      name: `Group ${index + 1}`,
      distinguishedName: `CN=Group ${index + 1}`,
    }));

    const summary = buildAdUserGroupsSummary(groups, 10);
    assert.equal(summary.totalCount, 12);
    assert.equal(summary.previewGroups.length, 10);
    assert.equal(summary.remainingCount, 2);
  });
});

describe("isGuidLike", () => {
  it("accepts canonical guid values", () => {
    assert.equal(isGuidLike("550e8400-e29b-41d4-a716-446655440000"), true);
    assert.equal(isGuidLike("not-a-guid"), false);
  });
});
