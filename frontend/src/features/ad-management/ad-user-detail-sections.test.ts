import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("AdUserDetailPage manager and expiration layout", () => {
  it("renders manager and expiration sections in a responsive grid", () => {
    const source = readFileSync(
      new URL("./AdUserDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /grid gap-4 xl:grid-cols-2/);
    assert.equal(source.includes("AdUserManagerSection"), true);
    assert.equal(source.includes("AdUserAccountExpirationSection"), true);
  });
});

describe("AdUserAccountExpirationSection", () => {
  it("uses project date picker and edit translation key", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserAccountExpirationSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    assert.equal(source.includes('type="date"'), false);
    assert.equal(source.includes("<DatePicker"), true);
    assert.equal(source.includes('common:actions.edit'), false);
    assert.equal(source.includes('adManagement:users.actions.edit'), true);
    assert.equal(source.includes("accountExpiresDate"), true);
    assert.equal(source.includes("AD_USER_FORM_ACTIONS_CLASSNAME"), true);
  });
});

describe("AdUserManagerSection form actions", () => {
  it("orders cancel before save and uses form action classname", () => {
    const source = readFileSync(
      new URL("./components/ad-user-detail/AdUserManagerSection.tsx", import.meta.url),
      "utf8",
    );

    assert.equal(source.includes("AD_USER_FORM_ACTIONS_CLASSNAME"), true);
    const cancelIndex = source.indexOf('common:actions.cancel');
    const saveIndex = source.indexOf('common:actions.save');
    assert.ok(cancelIndex > 0 && saveIndex > cancelIndex);
  });
});

describe("AdUserDetailPage group summary removal", () => {
  it("does not import or render AdUserGroupsSummarySection", () => {
    const source = readFileSync(
      new URL("./AdUserDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.equal(source.includes("AdUserGroupsSummarySection"), false);
  });
});

describe("invalidateAdUserDetailRelatedQueries", () => {
  it("invalidates detail, list and operation logs", () => {
    const source = readFileSync(new URL("./api.ts", import.meta.url), "utf8");
    assert.equal(source.includes("invalidateAdUserDetailRelatedQueries"), true);
    assert.match(source, /AD_MANAGEMENT_USERS_QUERY_KEY/);
    assert.match(source, /AD_OPERATION_LOGS_QUERY_KEY/);
  });
});
