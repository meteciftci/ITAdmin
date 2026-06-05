import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import { buildAdGroupDetailPath } from "./ad-group-detail-path.ts";
import {
  getAdGroupPrimaryLabel,
  getAdGroupSecondaryLabel,
} from "./ad-group-display-labels.ts";
import { AD_GROUPS_LIST_PATH } from "./ad-groups-list-path.ts";
import {
  buildAdGroupsListReturnState,
  resolveAdGroupReturnPath,
  resolveSafeAdGroupReturnPath,
} from "./ad-groups-return-path.ts";

const groupId = "550e8400-e29b-41d4-a716-446655440000";
const listPath = AD_GROUPS_LIST_PATH;

describe("ad groups navigation", () => {
  it("builds group detail path", () => {
    assert.equal(buildAdGroupDetailPath(groupId), `${listPath}/${groupId}`);
  });

  it("returns list path from list return state", () => {
    assert.equal(
      resolveAdGroupReturnPath(buildAdGroupsListReturnState()),
      listPath,
    );
  });

  it("falls back to groups list when return state is missing", () => {
    assert.equal(resolveAdGroupReturnPath(undefined), listPath);
  });

  it("rejects unsafe return paths", () => {
    assert.equal(resolveSafeAdGroupReturnPath("/ad-management/groups/../../../etc"), listPath);
  });
});

describe("ad group display labels", () => {
  it("uses displayName as primary label", () => {
    const group = {
      displayName: "VPN Users",
      name: "vpn-users",
      samAccountName: "vpn-users",
      distinguishedName: "CN=VPN Users,OU=Groups,DC=corp,DC=local",
    };

    assert.equal(getAdGroupPrimaryLabel(group), "VPN Users");
  });

  it("does not duplicate primary label in secondary label", () => {
    const group = {
      displayName: "VPN Users",
      name: "vpn-users",
      samAccountName: "vpn-users",
      distinguishedName: "CN=VPN Users,OU=Groups,DC=corp,DC=local",
    };
    const primary = getAdGroupPrimaryLabel(group);
    const secondary = getAdGroupSecondaryLabel(group, primary);

    assert.notEqual(secondary, primary);
    assert.equal(secondary, "vpn-users");
  });
});

describe("ad groups route and menu wiring", () => {
  it("protects groups routes with AdManagement.Groups.View permission", () => {
    const routerSource = readFileSync(
      new URL("../../app/router.tsx", import.meta.url),
      "utf8",
    );

    assert.match(routerSource, /path: "\/ad-management\/groups"/);
    assert.match(routerSource, /path: "\/ad-management\/groups\/:id"/);
    assert.match(routerSource, /RequirePermission permission="AdManagement\.Groups\.View"/);
  });

  it("shows groups menu item only for groups permission", () => {
    const sidebarSource = readFileSync(
      new URL("../../components/layout/sidebar-items.ts", import.meta.url),
      "utf8",
    );

    assert.match(sidebarSource, /AdManagement\.Groups\.View/);
    assert.match(sidebarSource, /to: "\/ad-management\/groups"/);
    assert.match(sidebarSource, /items\.adManagementGroups/);
  });

  it("renders groups list search input and read-only detail sections", () => {
    const toolbarSource = readFileSync(
      new URL("./components/AdGroupsSearchToolbar.tsx", import.meta.url),
      "utf8",
    );
    const columnsSource = readFileSync(
      new URL("./ad-groups-columns.tsx", import.meta.url),
      "utf8",
    );
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /searchPlaceholder=\{t\("adManagement:groups\.searchPlaceholder"\)\}/);
    assert.match(columnsSource, /getAdGroupPrimaryLabel/);
    assert.doesNotMatch(columnsSource, /groups\.actions\.(create|edit|delete)|AddUserToGroup|RemoveUserFromGroup/i);
    assert.match(detailSource, /getAdGroupPrimaryLabel/);
    assert.match(detailSource, /membersTruncated|memberOfTruncated/);
    assert.doesNotMatch(detailSource, /AddUserToGroup|RemoveUserFromGroup|manageGroups/i);
  });
});
