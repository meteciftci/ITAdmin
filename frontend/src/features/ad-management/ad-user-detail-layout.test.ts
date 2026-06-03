import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("AdUserAccountSummaryCards layout", () => {
  it("shows six summary cards without bad password count", () => {
    const source = readFileSync(
      new URL("./components/ad-user-detail/AdUserAccountSummaryCards.tsx", import.meta.url),
      "utf8",
    );

    assert.equal(source.includes("badPwdCount"), false);
    assert.equal(source.includes("accountExpires"), false);
    assert.equal(source.includes('key: "whenCreated"'), true);
    assert.equal(source.includes('key: "whenChanged"'), true);
    assert.match(source, /xl:grid-cols-3/);
  });
});

describe("AdUserTechnicalInfoSection layout", () => {
  it("does not duplicate account summary fields", () => {
    const source = readFileSync(
      new URL("./components/ad-user-detail/AdUserTechnicalInfoSection.tsx", import.meta.url),
      "utf8",
    );

    assert.equal(source.includes("users.detail.lastLogon"), false);
    assert.equal(source.includes("users.detail.passwordLastSet"), false);
    assert.equal(source.includes("users.detail.created"), false);
    assert.equal(source.includes("users.detail.changed"), false);
    assert.equal(source.includes("users.detail.page.badPwdCount"), true);
  });
});

describe("AdUserMappedAttributesSection layout", () => {
  it("uses switch instead of select filter", () => {
    const source = readFileSync(
      new URL("./components/ad-user-detail/AdUserMappedAttributesSection.tsx", import.meta.url),
      "utf8",
    );

    assert.equal(source.includes("<Switch"), true);
    assert.equal(source.includes("showEmptyFields"), true);
    assert.equal(source.includes("<Select"), false);
    assert.equal(source.includes("mappedFilter"), false);
  });
});
