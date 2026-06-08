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
  buildAdGroupDetailReturnState,
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

  it("returns detail path from detail return state", () => {
    assert.equal(
      resolveAdGroupReturnPath(buildAdGroupDetailReturnState(groupId)),
      `${listPath}/${groupId}`,
    );
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

  it("renders groups list search input, toolbar create action, and read-only detail sections", () => {
    const pageSource = readFileSync(
      new URL("./AdGroupsPage.tsx", import.meta.url),
      "utf8",
    );
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
    assert.match(toolbarSource, /canCreateGroup/);
    assert.match(toolbarSource, /groups\.create\.actions\.open/);
    assert.match(toolbarSource, /AD_GROUP_CREATE_PATH/);
    assert.doesNotMatch(pageSource, /SectionCard[\s\S]*actions=\{/);
    assert.match(pageSource, /canCreateGroup=\{canCreateGroup\}/);
    assert.match(columnsSource, /getAdGroupPrimaryLabel/);
    assert.match(columnsSource, /groups\.table\.group/);
    assert.match(columnsSource, /canUpdateGroup/);
    assert.match(columnsSource, /groups\.actions\.edit/);
    assert.doesNotMatch(columnsSource, /groups\.actions\.(create|delete)|AddUserToGroup|RemoveUserFromGroup/i);
    assert.match(detailSource, /getAdGroupPrimaryLabel/);
    assert.match(detailSource, /getAdGroupMemberPrimaryLabel/);
    assert.match(detailSource, /membersTruncated|memberOfTruncated/);
    assert.doesNotMatch(detailSource, /AddUserToGroup|RemoveUserFromGroup|manageGroups/i);
  });

  it("does not render separate name, cn, samAccountName, or distinguishedName list columns", () => {
    const columnsSource = readFileSync(
      new URL("./ad-groups-columns.tsx", import.meta.url),
      "utf8",
    );

    assert.doesNotMatch(columnsSource, /accessorKey: "name"/);
    assert.doesNotMatch(columnsSource, /accessorKey: "cn"/);
    assert.doesNotMatch(columnsSource, /accessorKey: "samAccountName"/);
    assert.doesNotMatch(columnsSource, /accessorKey: "distinguishedName"/);
    assert.doesNotMatch(columnsSource, /groups\.table\.distinguishedName/);
  });

  it("keeps DN in group cell title but not as visible column", () => {
    const columnsSource = readFileSync(
      new URL("./ad-groups-columns.tsx", import.meta.url),
      "utf8",
    );

    assert.match(columnsSource, /title=\{group\.distinguishedName\}/);
    assert.doesNotMatch(columnsSource, /font-mono[\s\S]*\{group\.distinguishedName\}/);
  });

  it("centers scope, type, and actions columns via DataTable meta align", () => {
    const columnsSource = readFileSync(
      new URL("./ad-groups-columns.tsx", import.meta.url),
      "utf8",
    );
    const scopeSection = columnsSource.slice(
      columnsSource.indexOf('id: "scope"'),
      columnsSource.indexOf('id: "type"'),
    );
    const typeSection = columnsSource.slice(
      columnsSource.indexOf('id: "type"'),
      columnsSource.indexOf('id: "actions"'),
    );
    const actionsSection = columnsSource.slice(
      columnsSource.indexOf('id: "actions"'),
      columnsSource.indexOf("];", columnsSource.indexOf('id: "actions"')),
    );

    assert.doesNotMatch(columnsSource, /CENTERED_ACTION_COLUMN_META/);
    assert.doesNotMatch(columnsSource, /CENTERED_COLUMN_META/);
    assert.doesNotMatch(columnsSource, /w-full text-center/);
    assert.doesNotMatch(columnsSource, /flex justify-center/);
    assert.match(scopeSection, /align: "center"/);
    assert.match(typeSection, /align: "center"/);
    assert.match(actionsSection, /isAction: true, align: "center"/);
    assert.match(actionsSection, /groups\.actions\.detail/);
    assert.match(actionsSection, /canUpdateGroup/);
    assert.match(actionsSection, /groups\.actions\.edit/);
    assert.doesNotMatch(actionsSection, /groups\.actions\.(create|delete)/);
  });

  it("navigates to edit from list with list return state and from detail with detail return state", () => {
    const pageSource = readFileSync(
      new URL("./AdGroupsPage.tsx", import.meta.url),
      "utf8",
    );
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /buildAdGroupsListReturnState\(\)/);
    assert.match(pageSource, /buildAdGroupEditPath/);
    assert.match(detailSource, /buildAdGroupDetailReturnState\(group\.id\)/);
    assert.match(detailSource, /adDetailEditButtonClass/);
    assert.match(detailSource, /adDetailOutlineButtonClass/);
    assert.match(detailSource, /adDetailActionButtonSizingClass/);
    assert.doesNotMatch(detailSource, /state=\{location\.state\}/);
  });

  it("keeps technical fields on group detail page", () => {
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /groups\.table\.displayName/);
    assert.match(detailSource, /groups\.table\.name/);
    assert.match(detailSource, /groups\.table\.cn/);
    assert.match(detailSource, /groups\.table\.samAccountName/);
    assert.match(detailSource, /groups\.table\.distinguishedName/);
  });

  it("does not render member distinguishedName as visible row", () => {
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );
    const memberListSource = detailSource.slice(
      detailSource.indexOf("function MemberList"),
      detailSource.indexOf("export function AdGroupDetailPage"),
    );

    assert.match(memberListSource, /getAdGroupMemberPrimaryLabel/);
    assert.match(memberListSource, /title=\{item\.distinguishedName\}/);
    assert.doesNotMatch(memberListSource, /font-mono[\s\S]*\{item\.distinguishedName\}/);
  });

  it("shows truncated notice with i18n key and warning styling", () => {
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /groups\.detail\.truncatedNotice/);
    assert.match(detailSource, /border-amber-500/);
  });

  it("uses simplified Turkish group labels in TR locale", () => {
    const trLocale = readFileSync(
      new URL("../../locales/tr/adManagement.json", import.meta.url),
      "utf8",
    );

    assert.match(trLocale, /"whenCreated": "Oluşturulma Tarihi"/);
    assert.match(trLocale, /"whenChanged": "Değiştirilme Tarihi"/);
    assert.match(trLocale, /"managedBy": "Yönetici"/);
    assert.match(trLocale, /"displayName": "Grup Görünen Ad"/);
    assert.match(trLocale, /"technicalName": "Grup Adı"/);
    assert.match(trLocale, /"name": "Grup Adı"/);
    assert.doesNotMatch(trLocale, /Grup Teknik Adı/);
    assert.doesNotMatch(trLocale, /"whenCreated": "When Created"/);
    assert.doesNotMatch(trLocale, /"whenChanged": "When Changed"/);
    assert.doesNotMatch(trLocale, /"managedBy": "Managed By"/);
  });
});
