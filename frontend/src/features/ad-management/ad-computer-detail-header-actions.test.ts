import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

function readHeaderActionsSource(): string {
  return readFileSync(
    new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url),
    "utf8",
  );
}

function extractRowActionsBlock(source: string): string {
  const start = source.indexOf("<RowActions");
  assert.notEqual(start, -1, "RowActions block should exist");
  const end = source.indexOf("</RowActions>", start);
  assert.notEqual(end, -1, "RowActions closing tag should exist");
  return source.slice(start, end + "</RowActions>".length);
}

describe("ad computer detail header actions", () => {
  it("keeps back, refresh and edit as direct header actions", () => {
    const source = readHeaderActionsSource();

    assert.match(source, /Link to=\{returnPath\}/);
    assert.match(source, /onClick=\{onRefresh\}/);
    assert.match(source, /adDetailEditButtonClass/);
    assert.match(source, /common:actions\.edit/);
  });

  it("places manage groups navigation inside RowActions instead of a direct button", () => {
    const source = readHeaderActionsSource();
    const rowActionsBlock = extractRowActionsBlock(source);

    assert.match(rowActionsBlock, /canManageGroups/);
    assert.match(rowActionsBlock, /buildAdComputerGroupsPath/);
    assert.match(rowActionsBlock, /buildAdComputerDetailReturnState/);
    assert.match(rowActionsBlock, /computers\.actions\.manageGroups/);
    assert.doesNotMatch(
      source,
      /canManageGroups\s*\?\s*\(\s*<Button[\s\S]*buildAdComputerGroupsPath/,
    );
  });

  it("places move OU navigation inside RowActions instead of a direct button", () => {
    const source = readHeaderActionsSource();
    const rowActionsBlock = extractRowActionsBlock(source);

    assert.match(rowActionsBlock, /showMoveOu/);
    assert.match(rowActionsBlock, /buildAdComputerMoveOuPath/);
    assert.match(rowActionsBlock, /computers\.actions\.moveOu/);
    assert.doesNotMatch(
      source,
      /showMoveOu\s*\?\s*\(\s*<Button[\s\S]*buildAdComputerMoveOuPath/,
    );
  });

  it("keeps enable, disable and delete actions inside RowActions", () => {
    const source = readHeaderActionsSource();
    const rowActionsBlock = extractRowActionsBlock(source);

    assert.match(rowActionsBlock, /showEnable/);
    assert.match(rowActionsBlock, /showDisable/);
    assert.match(rowActionsBlock, /showDelete/);
    assert.match(rowActionsBlock, /computers\.actions\.enable/);
    assert.match(rowActionsBlock, /computers\.actions\.disable/);
    assert.match(rowActionsBlock, /text-destructive focus:text-destructive/);
    assert.match(rowActionsBlock, /common:actions\.delete/);
    assert.match(source, /AdComputerDeleteConfirmDialog/);
    assert.match(source, /ConfirmDialog/);
    assert.match(source, /AdComputerUpdateDescriptionDialog/);
  });

  it("shows operations menu only when at least one action is available", () => {
    const source = readHeaderActionsSource();

    assert.match(source, /hasMembershipActions/);
    assert.match(source, /hasAccountStatusActions/);
    assert.match(source, /hasOperations/);
    assert.match(source, /hasOperations \?/);
  });
});

describe("ad detail action pattern consistency", () => {
  it("keeps user detail operations in RowActions", () => {
    const source = readFileSync(
      new URL("./components/ad-user-detail/AdUserDetailHeaderActions.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /hasOperations/);
    assert.match(source, /users\.actions\.manageGroups/);
    assert.match(source, /users\.actions\.moveOu/);
    assert.doesNotMatch(
      source,
      /canManageGroups\s*\?\s*\(\s*<Button[\s\S]*\/groups/,
    );
  });

  it("keeps group detail operations in RowActions", () => {
    const source = readFileSync(new URL("./AdGroupDetailPage.tsx", import.meta.url), "utf8");

    assert.match(source, /groups\.detail\.actions\.operations/);
    assert.match(source, /groups\.actions\.manageMembers/);
    assert.match(source, /groups\.actions\.moveOu/);
    assert.doesNotMatch(
      source,
      /canManageMembers\s*\?\s*\(\s*<Button[\s\S]*buildAdGroupMembersPath/,
    );
  });
});

describe("ad computer detail header action i18n", () => {
  it("aligns user and computer manageGroups labels in TR and EN", () => {
    const tr = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    );
    const en = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    );

    assert.equal(
      tr.adManagement.users.actions.manageGroups,
      tr.adManagement.computers.actions.manageGroups,
    );
    assert.equal(
      en.adManagement.users.actions.manageGroups,
      en.adManagement.computers.actions.manageGroups,
    );
    assert.equal(tr.adManagement.users.actions.manageGroups, "Grup üyeliklerini yönet");
    assert.equal(en.adManagement.users.actions.manageGroups, "Manage group memberships");
  });

  it("keeps group manageMembers labels distinct from user/computer manageGroups", () => {
    const tr = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    );
    const en = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    );

    assert.notEqual(
      tr.adManagement.groups.actions.manageMembers,
      tr.adManagement.users.actions.manageGroups,
    );
    assert.notEqual(
      en.adManagement.groups.actions.manageMembers,
      en.adManagement.users.actions.manageGroups,
    );
  });
});
