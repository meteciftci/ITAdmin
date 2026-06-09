import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("Ad group member management UI", () => {
  it("describes member management purpose in section card description", () => {
    const sectionSource = readFileSync(
      new URL("./components/AdGroupMembersSection.tsx", import.meta.url),
      "utf8",
    );
    const trLocale = readFileSync(
      new URL("../../locales/tr/adManagement.json", import.meta.url),
      "utf8",
    );
    const enLocale = readFileSync(
      new URL("../../locales/en/adManagement.json", import.meta.url),
      "utf8",
    );

    assert.match(sectionSource, /groups\.members\.descriptionManage/);
    assert.match(sectionSource, /groups\.members\.descriptionView/);
    assert.match(sectionSource, /groups\.detail\.memberCount/);
    assert.match(sectionSource, /tabIndex=\{-1\}/);
    assert.match(sectionSource, /forwardRef/);
    assert.match(trLocale, /"descriptionManage": "Doğrudan grup üyelerini görüntüleyin, ekleyin veya çıkarın."/);
    assert.match(enLocale, /"descriptionManage": "View, add, or remove direct group members."/);
  });

  it("shows add member action only when ManageMembers permission is available", () => {
    const sectionSource = readFileSync(
      new URL("./components/AdGroupMembersSection.tsx", import.meta.url),
      "utf8",
    );
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(sectionSource, /canManageMembers/);
    assert.match(sectionSource, /groups\.members\.add/);
    assert.match(sectionSource, /groups\.members\.remove/);
    assert.match(detailSource, /canManageMembers=\{canManageMembers\}/);
    assert.match(detailSource, /groups\.actions\.manageMembers/);
    assert.match(detailSource, /scrollToMembersSection/);
    assert.doesNotMatch(detailSource, /groups\.members\.add/);
  });

  it("scrolls members section through app layout container with scheduled deep-link retries", () => {
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /searchParams\.get\("section"\)/);
    assert.match(detailSource, /data-app-scroll-container='true'/);
    assert.match(detailSource, /scrollElementIntoAppContainer/);
    assert.match(detailSource, /scrollContainer\.scrollTo/);
    assert.match(detailSource, /scrollIntoView\(\{ behavior, block: "start" \}\)/);
    assert.match(detailSource, /const scheduleScroll = \(delay: number, behavior: ScrollBehavior\)/);
    assert.match(detailSource, /scheduleScroll\(0, "auto"\)/);
    assert.match(detailSource, /scheduleScroll\(600, "auto"\)/);
    assert.match(
      detailSource,
      /if \(didScroll && delay >= 300\) \{[\s\S]*lastMembersScrollKeyRef\.current = navigationKey/,
    );
    assert.match(detailSource, /scrollToMembersSection\("smooth"\)/);
    assert.match(detailSource, /if \(!element\) \{[\s\S]*return false/);
  });

  it("uses member column header in members list and keeps selectCandidate in add dialog only", () => {
    const sectionSource = readFileSync(
      new URL("./components/AdGroupMembersSection.tsx", import.meta.url),
      "utf8",
    );
    const dialogSource = readFileSync(
      new URL("./components/AdAddGroupMemberDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(sectionSource, /groups\.members\.memberColumn/);
    assert.doesNotMatch(sectionSource, /groups\.members\.selectCandidate/);
    assert.match(dialogSource, /groups\.members\.selectCandidate/);
  });

  it("renders member-specific search placeholder and range info", () => {
    const sectionSource = readFileSync(
      new URL("./components/AdGroupMembersSection.tsx", import.meta.url),
      "utf8",
    );

    assert.match(sectionSource, /groups\.members\.searchPlaceholder/);
    assert.match(sectionSource, /groups\.members\.rangeInfo/);
    assert.doesNotMatch(sectionSource, /users\.groups\.pagination\.rangeInfo/);
    assert.match(sectionSource, /groups\.members\.searchNoResults/);
  });

  it("keeps remove action behind confirmation and permission guard", () => {
    const sectionSource = readFileSync(
      new URL("./components/AdGroupMembersSection.tsx", import.meta.url),
      "utf8",
    );

    assert.match(sectionSource, /if \(canManageMembers\)/);
    assert.match(sectionSource, /setRemoveTarget/);
    assert.match(sectionSource, /AdRemoveGroupMemberConfirmDialog/);
    assert.match(sectionSource, /variant="outline"/);
  });

  it("uses server-side member query params for search, type, and pagination", () => {
    const sectionSource = readFileSync(
      new URL("./components/AdGroupMembersSection.tsx", import.meta.url),
      "utf8",
    );
    const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");

    assert.match(sectionSource, /getAdGroupMembers/);
    assert.match(sectionSource, /typeFilter/);
    assert.match(sectionSource, /pageNumber/);
    assert.match(sectionSource, /pageSize/);
    assert.match(apiSource, /\/ad-management\/groups\/\$\{groupId\}\/members/);
    assert.match(apiSource, /member-candidates/);
  });

  it("uses DialogBody for add member dialog content spacing", () => {
    const dialogSource = readFileSync(
      new URL("./components/AdAddGroupMemberDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(dialogSource, /DialogBody/);
    assert.doesNotMatch(
      dialogSource,
      /<DialogHeader>[\s\S]*?<div className="space-y-4">/,
    );
  });

  it("requires minimum search length before candidate query in add dialog", () => {
    const dialogSource = readFileSync(
      new URL("./components/AdAddGroupMemberDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(dialogSource, /MIN_SEARCH_LENGTH = 2/);
    assert.match(dialogSource, /search\.trim\(\)\.length >= MIN_SEARCH_LENGTH/);
    assert.match(dialogSource, /!selectedCandidate \|\| addMutation\.isPending/);
    assert.match(dialogSource, /invalidateAdGroupMemberQueries/);
  });

  it("shows direct membership warning in remove confirmation dialog", () => {
    const dialogSource = readFileSync(
      new URL("./components/AdRemoveGroupMemberConfirmDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(dialogSource, /groups\.members\.removeDescription/);
    assert.match(dialogSource, /variant="destructive"/);
  });

  it("renders GroupMemberAdd logs with dedicated groupMember strategy", () => {
    const snapshotDetailSource = readFileSync(
      new URL("./components/AdOperationLogSnapshotDetail.tsx", import.meta.url),
      "utf8",
    );

    assert.match(snapshotDetailSource, /case "groupMember"/);
    assert.match(snapshotDetailSource, /GroupMemberSnapshotSections/);
    assert.match(
      snapshotDetailSource,
      /case "groupMember":[\s\S]*?GroupMemberSnapshotSections/,
    );
    assert.doesNotMatch(
      snapshotDetailSource,
      /case "groupMember":[\s\S]*?GenericSnapshotBlock/,
    );
  });
});
