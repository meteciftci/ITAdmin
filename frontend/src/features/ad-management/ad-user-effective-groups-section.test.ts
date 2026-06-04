import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("AdUserEffectiveGroupsSection", () => {
  it("uses effective groups query key with user id and max depth", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserEffectiveGroupsSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    assert.equal(source.includes("AD_MANAGEMENT_USER_EFFECTIVE_GROUPS_QUERY_KEY"), true);
    assert.equal(source.includes("userId, maxDepth"), true);
    assert.equal(source.includes("getAdUserEffectiveGroups"), true);
  });

  it("renders direct and effective tabs with path breadcrumb", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserEffectiveGroupsSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    assert.equal(source.includes("MembershipPathBreadcrumb"), true);
    assert.equal(source.includes('value="direct"'), true);
    assert.equal(source.includes('value="effective"'), true);
    assert.equal(source.includes("truncated"), true);
    assert.equal(source.includes("<LoadingState"), true);
  });
});

describe("AdUserDetailPage effective groups visibility", () => {
  it("shows effective groups section only when groups view permission is granted", () => {
    const source = readFileSync(
      new URL("./AdUserDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /canManageGroups \? \([\s\S]*AdUserEffectiveGroupsSection/);
    assert.equal(source.includes("AdManagement.Users.Groups.View"), true);
  });
});
