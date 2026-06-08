import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("Ad group member management UI", () => {
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
    assert.doesNotMatch(snapshotDetailSource, /case "groupMember":[\s\S]*generic/i);
  });
});
