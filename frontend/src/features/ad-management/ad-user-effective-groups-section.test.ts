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
          searchPlaceholder: string;
          directCountFiltered: string;
          empty: { searchTitle: string };
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

  it("includes search input and client-side filter helpers", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserEffectiveGroupsSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    assert.equal(source.includes("searchQuery"), true);
    assert.equal(source.includes("filterEffectiveGroupMemberships"), true);
    assert.equal(source.includes("searchPlaceholder"), true);
    assert.equal(source.includes("directCountFiltered"), true);
    assert.equal(source.includes("empty.searchTitle"), true);
    assert.equal(source.includes("filteredMemberships.directGroups"), true);
    assert.equal(source.includes("filteredMemberships.effectiveGroups"), true);
  });

  it("renders search input after TabsList with full-width wrapper", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserEffectiveGroupsSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    const tabsListIndex = source.indexOf("<TabsList");
    const searchWrapperIndex = source.indexOf('className="relative mt-3 w-full"');
    const tabsContentIndex = source.indexOf('<TabsContent value="direct"');

    assert.ok(tabsListIndex > 0);
    assert.ok(searchWrapperIndex > tabsListIndex);
    assert.ok(tabsContentIndex > searchWrapperIndex);
    assert.equal(source.includes("relative max-w-md"), false);
    assert.equal(source.includes("filterEffectiveGroupMemberships"), true);
    assert.equal(source.includes("directCountFiltered"), true);
  });

  it("uses translated tab labels from locale at users.detail.effectiveGroups path", () => {
    const effectiveGroups = enAdManagement.adManagement.users.detail.effectiveGroups;

    assert.equal(effectiveGroups.tabs.direct, "Direct");
    assert.equal(effectiveGroups.tabs.effective, "Nested / Effective");
    assert.equal(effectiveGroups.searchPlaceholder, "Search groups...");
    assert.equal(effectiveGroups.directCountFiltered, "Direct groups: {{filtered}} / {{total}}");
    assert.equal(effectiveGroups.empty.searchTitle, "No search results");
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
    assert.equal(source.includes("break-all font-mono"), true);
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

  it("does not render AdUserGroupsSummarySection", () => {
    const source = readFileSync(
      new URL("./AdUserDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.equal(source.includes("AdUserGroupsSummarySection"), false);
  });
});
