import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  doesDirectGroupMatchSearch,
  doesEffectiveGroupMatchSearch,
  filterDirectGroupsBySearch,
  filterEffectiveGroupMemberships,
  normalizeEffectiveGroupSearchText,
} from "./ad-effective-group-search.ts";
import type {
  AdEffectiveGroupNestedItem,
  AdEffectiveGroupSummaryItem,
} from "./types.ts";

function createDirectGroup(
  overrides: Partial<AdEffectiveGroupSummaryItem> = {},
): AdEffectiveGroupSummaryItem {
  return {
    name: "VPN_Users",
    distinguishedName: "CN=VPN_Users,OU=Groups,DC=example,DC=com",
    samAccountName: "VPN_Users",
    description: null,
    displayName: "VPN Users",
    ...overrides,
  };
}

function createEffectiveGroup(
  overrides: Partial<AdEffectiveGroupNestedItem> = {},
): AdEffectiveGroupNestedItem {
  return {
    ...createDirectGroup(),
    depth: 2,
    isDirect: false,
    path: [
      {
        type: "User",
        name: "Mete TEST",
        displayName: "Mete TEST",
        samAccountName: "mete.test",
        distinguishedName: "CN=Mete TEST,OU=Users,DC=example,DC=com",
      },
      {
        type: "Group",
        name: "BilgiIslem_Users",
        displayName: "Bilgi Islem Users",
        samAccountName: "BilgiIslem_Users",
        distinguishedName: "CN=BilgiIslem_Users,OU=Groups,DC=example,DC=com",
      },
      {
        type: "Group",
        name: "VPN_Users",
        displayName: "VPN Users",
        samAccountName: "VPN_Users",
        distinguishedName: "CN=VPN_Users,OU=Groups,DC=example,DC=com",
      },
    ],
    ...overrides,
  };
}

describe("ad-effective-group-search", () => {
  it("normalizes search text with trim and Turkish locale", () => {
    assert.equal(normalizeEffectiveGroupSearchText("  VPN  "), "vpn");
    assert.equal(normalizeEffectiveGroupSearchText("İSTANBUL"), "istanbul");
  });

  it("returns all direct groups when query is empty", () => {
    const groups = [createDirectGroup(), createDirectGroup({ name: "Other" })];
    assert.equal(filterDirectGroupsBySearch(groups, "").length, 2);
    assert.equal(filterDirectGroupsBySearch(groups, "   ").length, 2);
  });

  it("matches direct groups by displayName, name, samAccountName, description and DN", () => {
    const group = createDirectGroup({
      displayName: "Finance Team",
      name: "Finance_Team",
      samAccountName: "finance.team",
      description: "Finance department access",
      distinguishedName: "CN=Finance_Team,OU=Groups,DC=example,DC=com",
    });

    assert.equal(doesDirectGroupMatchSearch(group, "finance team"), true);
    assert.equal(doesDirectGroupMatchSearch(group, "finance_team"), true);
    assert.equal(doesDirectGroupMatchSearch(group, "finance.team"), true);
    assert.equal(doesDirectGroupMatchSearch(group, "department access"), true);
    assert.equal(doesDirectGroupMatchSearch(group, "CN=Finance_Team"), true);
    assert.equal(doesDirectGroupMatchSearch(group, "unknown"), false);
  });

  it("matches effective groups by path node values", () => {
    const group = createEffectiveGroup();

    assert.equal(doesEffectiveGroupMatchSearch(group, "vpn users"), true);
    assert.equal(doesEffectiveGroupMatchSearch(group, "bilgi islem"), true);
    assert.equal(doesEffectiveGroupMatchSearch(group, "BilgiIslem_Users"), true);
    assert.equal(doesEffectiveGroupMatchSearch(group, "mete.test"), true);
    assert.equal(doesEffectiveGroupMatchSearch(group, "missing-group"), false);
  });

  it("filters memberships for direct and effective lists", () => {
    const data = {
      directGroups: [
        createDirectGroup({ name: "Alpha" }),
        createDirectGroup({ name: "Beta" }),
      ],
      effectiveGroups: [
        createEffectiveGroup({ name: "Gamma" }),
        createEffectiveGroup({
          name: "Delta",
          path: [
            {
              type: "Group",
              name: "ParentGroup",
              displayName: null,
              samAccountName: null,
              distinguishedName: "CN=ParentGroup,OU=Groups,DC=example,DC=com",
            },
          ],
        }),
      ],
    };

    const filtered = filterEffectiveGroupMemberships(data, "parentgroup");

    assert.equal(filtered.directGroups.length, 0);
    assert.equal(filtered.effectiveGroups.length, 1);
    assert.equal(filtered.effectiveGroups[0]?.name, "Delta");
  });
});
