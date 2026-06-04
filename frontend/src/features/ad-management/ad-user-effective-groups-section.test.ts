import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const enAdManagement = JSON.parse(
  readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
) as {
  adManagement: {
    users: {
      detail: {
        effectiveGroups: {
          tabs: { direct: string; effective: string };
        };
      };
    };
  };
};

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

  it("does not render raw i18n key paths in tab labels", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserEffectiveGroupsSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    assert.equal(source.includes("users.detail.effectiveGroups.tabs.direct"), true);
    assert.equal(source.includes('"users.detail.effectiveGroups.tabs.direct"'), false);
    assert.equal(source.includes(">users.detail.effectiveGroups"), false);
  });

  it("uses translated tab labels from locale at users.detail.effectiveGroups path", () => {
    const tabs = enAdManagement.adManagement.users.detail.effectiveGroups.tabs;

    assert.equal(tabs.direct, "Direct");
    assert.equal(tabs.effective, "Nested / Effective");
  });

  it("renders group cards with shared label helpers and DN field", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserEffectiveGroupsSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    assert.equal(source.includes("getAdGroupPrimaryLabel"), true);
    assert.equal(source.includes("getAdGroupSecondaryLabel"), true);
    assert.equal(source.includes("break-all font-mono"), true);
    assert.equal(source.includes("MembershipPathBreadcrumb"), true);
    assert.equal(source.includes("<LoadingState"), true);
  });

  it("does not show duplicate name and samAccountName when they match primary", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserEffectiveGroupsSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    assert.equal(source.includes("{group.name}"), false);
    assert.equal(source.includes("{group.samAccountName}"), false);
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

  it("does not render AdUserGroupsSummarySection", () => {
    const source = readFileSync(
      new URL("./AdUserDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.equal(source.includes("AdUserGroupsSummarySection"), false);
  });
});
