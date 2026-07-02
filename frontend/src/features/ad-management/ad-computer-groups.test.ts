import assert from "node:assert/strict";
import { readAdManagementApiSource } from "./api/api-source.test-support.ts";
import { readRouterSource } from "../../app/routes/route-source.test-support.ts";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import { buildAdComputerGroupsPath } from "./ad-computer-detail-path.ts";
import { AD_COMPUTERS_LIST_PATH } from "./ad-computers-list-path.ts";
import { getSnapshotRenderStrategy } from "./parse-ad-operation-snapshot.ts";

const computerId = "550e8400-e29b-41d4-a716-446655440000";

describe("ad computer groups navigation", () => {
  it("builds computer groups path", () => {
    assert.equal(
      buildAdComputerGroupsPath(computerId),
      `${AD_COMPUTERS_LIST_PATH}/${computerId}/groups`,
    );
  });
});

describe("ad computer groups source inspection", () => {
  it("registers protected groups route with permission guard", () => {
    const routerSource = readRouterSource();
    assert.match(routerSource, /\/ad-management\/computers\/:id\/groups/);
    assert.match(routerSource, /AdManagement\.Computers\.Groups\.View/);
    assert.match(routerSource, /AdComputerGroupsPage/);
  });

  it("detail page wires groups permission to header action", () => {
    const detailSource = readFileSync(new URL("./AdComputerDetailPage.tsx", import.meta.url), "utf8");
    assert.match(detailSource, /AdManagement\.Computers\.Groups\.View/);
    assert.match(detailSource, /canManageGroups/);
  });

  it("detail header action navigates to groups route from operations menu", () => {
    const actionsSource = readFileSync(
      new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url),
      "utf8",
    );
    const rowActionsBlock = actionsSource.slice(
      actionsSource.indexOf("<RowActions"),
      actionsSource.indexOf("</RowActions>") + "</RowActions>".length,
    );

    assert.match(rowActionsBlock, /canManageGroups/);
    assert.match(rowActionsBlock, /buildAdComputerGroupsPath/);
    assert.match(rowActionsBlock, /manageGroups/);
    assert.doesNotMatch(
      actionsSource,
      /canManageGroups\s*\?\s*\(\s*<Button[\s\S]*buildAdComputerGroupsPath/,
    );
  });

  it("groups page uses computer detail and groups queries", () => {
    const pageSource = readFileSync(new URL("./AdComputerGroupsPage.tsx", import.meta.url), "utf8");
    assert.match(pageSource, /getAdComputerById/);
    assert.match(pageSource, /getAdComputerGroups/);
    assert.match(pageSource, /AdComputerGroupMultiSearchCombobox/);
    const comboboxSource = readFileSync(
      new URL("./components/AdComputerGroupMultiSearchCombobox.tsx", import.meta.url),
      "utf8",
    );
    assert.match(comboboxSource, /searchAdComputerGroupCandidates/);
    assert.match(comboboxSource, /AD_COMBOBOX_POPOVER_CONTENT_PROPS/);
    assert.match(comboboxSource, /autoFocus/);
    assert.match(pageSource, /removeAdComputerFromGroup/);
    assert.match(pageSource, /invalidateAdComputerGroupsQuery/);
    assert.match(pageSource, /ConfirmDialog/);
  });

  it("api functions call expected endpoints", () => {
    const apiSource = readAdManagementApiSource();
    assert.match(apiSource, /`\/ad-management\/computers\/\$\{computerId\}\/groups`/);
    assert.match(apiSource, /`\/ad-management\/computers\/\$\{computerId\}\/group-candidates`/);
    assert.match(apiSource, /addAdComputerToGroup/);
    assert.match(apiSource, /removeAdComputerFromGroup/);
  });

  it("snapshot strategy supports computer group membership operations", () => {
    assert.equal(getSnapshotRenderStrategy("ComputerGroupAdd"), "groupMembership");
    assert.equal(getSnapshotRenderStrategy("ComputerGroupRemove"), "groupMembership");
  });

  it("snapshot renderer shows computer section for computer group operations", () => {
    const detailSource = readFileSync(
      new URL("./components/AdOperationLogSnapshotDetail.tsx", import.meta.url),
      "utf8",
    );
    assert.match(detailSource, /ComputerGroupAdd/);
    assert.match(detailSource, /resolveSnapshotComputer/);
    assert.match(detailSource, /getComputerFieldEntries/);
  });
});

describe("ad computer groups i18n", () => {
  it("includes TR computer groups keys", () => {
    const tr = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    );
    assert.equal(tr.adManagement.computers.actions.manageGroups, "Grup üyeliklerini yönet");
    assert.equal(
      tr.adManagement.users.actions.manageGroups,
      tr.adManagement.computers.actions.manageGroups,
    );
    assert.equal(tr.adManagement.computers.groups.actions.addToGroup, "Gruba Ekle");
    assert.equal(tr.adManagement.computers.groups.protected, "Bu bilgisayar hesabında grup üyeliği değiştirilemez.");
  });

  it("includes EN computer groups keys", () => {
    const en = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    );
    assert.equal(en.adManagement.computers.actions.manageGroups, "Manage group memberships");
    assert.equal(
      en.adManagement.users.actions.manageGroups,
      en.adManagement.computers.actions.manageGroups,
    );
    assert.equal(en.adManagement.computers.groups.actions.addToGroup, "Add to Group");
  });

  it("includes operation log labels for computer group operations", () => {
    const trLogs = JSON.parse(
      readFileSync(new URL("../../locales/tr/adOperationLogs.json", import.meta.url), "utf8"),
    );
    const enLogs = JSON.parse(
      readFileSync(new URL("../../locales/en/adOperationLogs.json", import.meta.url), "utf8"),
    );
    assert.equal(trLogs.adOperationLogs.operations.ComputerGroupAdd, "Bilgisayarı gruba ekleme");
    assert.equal(trLogs.adOperationLogs.operations.ComputerGroupRemove, "Bilgisayarı gruptan çıkarma");
    assert.equal(enLogs.adOperationLogs.operations.ComputerGroupAdd, "Add computer to group");
    assert.equal(enLogs.adOperationLogs.operations.ComputerGroupRemove, "Remove computer from group");
  });
});
