import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("ad membership multi-select", () => {
  it("adds manage groups row action with permission and route", () => {
    const columnsSource = readFileSync(
      new URL("./ad-computers-columns.tsx", import.meta.url),
      "utf8",
    );
    const pageSource = readFileSync(new URL("./AdComputersPage.tsx", import.meta.url), "utf8");

    assert.match(columnsSource, /canManageGroups/);
    assert.match(columnsSource, /onManageGroups/);
    assert.match(columnsSource, /computers\.actions\.manageGroups/);
    assert.match(pageSource, /AdManagement\.Computers\.Groups\.View/);
    assert.match(pageSource, /buildAdComputerGroupsPath/);
    assert.match(pageSource, /onManageGroups:/);
    assert.match(columnsSource, /common:actions\.detail/);
    assert.match(columnsSource, /computers\.actions\.moveOu/);
    assert.match(columnsSource, /computers\.actions\.disable/);
    assert.match(columnsSource, /computers\.actions\.enable/);
    assert.match(columnsSource, /common:actions\.delete/);
  });

  it("aligns computer group multi search combobox with user group combobox behavior", () => {
    const comboboxSource = readFileSync(
      new URL("./components/AdComputerGroupMultiSearchCombobox.tsx", import.meta.url),
      "utf8",
    );

    assert.match(comboboxSource, /AD_COMBOBOX_POPOVER_CONTENT_PROPS/);
    assert.match(comboboxSource, /AD_COMBOBOX_TRIGGER_LABEL_CLASSNAME/);
    assert.match(comboboxSource, /autoFocus/);
    assert.doesNotMatch(comboboxSource, /w-\[var\(--radix-popover-trigger-width\)\]/);
    assert.match(comboboxSource, /membershipMultiSelect\.alreadyDirectGroupMember/);
  });

  it("uses multi-select state and sequential add on user groups page", () => {
    const pageSource = readFileSync(new URL("./AdUserGroupsPage.tsx", import.meta.url), "utf8");

    assert.match(pageSource, /selectedGroups/);
    assert.match(pageSource, /AdGroupMultiSearchCombobox/);
    assert.match(pageSource, /AdMembershipSelectionChips/);
    assert.match(pageSource, /runSequentialMembershipAdd/);
    assert.match(pageSource, /addAdUserToGroup/);
    assert.match(pageSource, /membershipMultiSelect\.partialSuccess/);
    assert.match(pageSource, /membershipMultiSelect\.addSelected/);
  });

  it("uses multi-select state and sequential add on computer groups page", () => {
    const pageSource = readFileSync(
      new URL("./AdComputerGroupsPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /selectedGroups/);
    assert.match(pageSource, /AdComputerGroupMultiSearchCombobox/);
    assert.match(pageSource, /runSequentialMembershipAdd/);
    assert.match(pageSource, /addAdComputerToGroup/);
    assert.match(pageSource, /membershipMultiSelect\.partialSuccess/);
  });

  it("uses multi-select state and sequential add in group member dialog", () => {
    const dialogSource = readFileSync(
      new URL("./components/AdAddGroupMemberDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(dialogSource, /selectedCandidates/);
    assert.match(dialogSource, /AdMembershipSelectionChips/);
    assert.match(dialogSource, /runSequentialMembershipAdd/);
    assert.match(dialogSource, /addAdGroupMember/);
    assert.match(dialogSource, /isAlreadyDirectMember/);
    assert.match(dialogSource, /membershipMultiSelect\.alreadyDirectMember/);
    assert.match(dialogSource, /membershipMultiSelect\.allMembersAdded/);
  });

  it("keeps failed selections and clears successful ones after partial add", () => {
    const userPageSource = readFileSync(new URL("./AdUserGroupsPage.tsx", import.meta.url), "utf8");
    const computerPageSource = readFileSync(
      new URL("./AdComputerGroupsPage.tsx", import.meta.url),
      "utf8",
    );
    const dialogSource = readFileSync(
      new URL("./components/AdAddGroupMemberDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(userPageSource, /failed\.some/);
    assert.match(computerPageSource, /failed\.some/);
    assert.match(dialogSource, /failed\.some/);
    assert.match(userPageSource, /notifySequentialAddResults/);
    assert.match(computerPageSource, /notifySequentialAddResults/);
    assert.match(dialogSource, /notifySequentialAddResults/);
  });

  it("includes TR and EN membership multi-select locale keys", () => {
    const tr = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    );
    const en = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    );

    assert.equal(tr.adManagement.membershipMultiSelect.selectedGroups, "Seçilen gruplar");
    assert.equal(tr.adManagement.membershipMultiSelect.addSelected, "Seçilenleri ekle");
    assert.equal(
      tr.adManagement.membershipMultiSelect.partialSuccess,
      "{{successCount}} kayıt eklendi, {{failedCount}} kayıt eklenemedi.",
    );
    assert.equal(en.adManagement.membershipMultiSelect.selectedMembers, "Selected members");
    assert.equal(en.adManagement.membershipMultiSelect.allGroupsAdded, "All selected groups were added.");
    assert.equal(
      en.adManagement.membershipMultiSelect.alreadyDirectMember,
      "This member is already a direct member.",
    );
  });
});
