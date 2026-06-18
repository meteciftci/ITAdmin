import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import { buildAdGroupDetailPath, buildAdGroupMoveOuPath } from "./ad-group-detail-path.ts";
import { getSnapshotRenderStrategy } from "./parse-ad-operation-snapshot.ts";

const groupId = "550e8400-e29b-41d4-a716-446655440000";

describe("AD group move OU navigation", () => {
  it("builds group move OU path", () => {
    assert.equal(
      buildAdGroupMoveOuPath(groupId),
      `/ad-management/groups/${groupId}/move-ou`,
    );
  });

  it("uses GroupMoveOu dedicated snapshot strategy", () => {
    assert.equal(getSnapshotRenderStrategy("GroupMoveOu"), "ouMove");
    assert.notEqual(getSnapshotRenderStrategy("GroupMoveOu"), "generic");
  });

  it("keeps UserOuMove on ouMove strategy", () => {
    assert.equal(getSnapshotRenderStrategy("UserOuMove"), "ouMove");
  });
});

describe("AdMoveGroupOuPage wiring", () => {
  it("disables submit until target OU is selected and blocks same OU", () => {
    const pageSource = readFileSync(
      new URL("./AdMoveGroupOuPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /targetOuDistinguishedName/);
    assert.match(pageSource, /sameOuWarning/);
    assert.match(pageSource, /groups\.moveOu\.sameOu/);
    assert.match(pageSource, /disabled=\{!canSubmit\}/);
    assert.match(pageSource, /invalidateAdGroupOuMoveQueries/);
    assert.match(pageSource, /searchContext="groups"/);
  });

  it("returns to group detail after success", () => {
    const pageSource = readFileSync(
      new URL("./AdMoveGroupOuPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /buildAdGroupDetailPath\(groupId\)/);
    assert.match(pageSource, /groups\.moveOu\.success/);
  });
});

describe("group list and detail move OU actions", () => {
  it("shows move OU in list actions when permission is granted", () => {
    const columnsSource = readFileSync(
      new URL("./ad-groups-columns.tsx", import.meta.url),
      "utf8",
    );
    const pageSource = readFileSync(
      new URL("./AdGroupsPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(columnsSource, /canMoveOu/);
    assert.match(columnsSource, /groups\.actions\.moveOu/);
    assert.match(columnsSource, /onMoveOu/);
    assert.match(pageSource, /AdManagement\.Groups\.MoveOu/);
    assert.match(pageSource, /buildAdGroupMoveOuPath/);
    assert.doesNotMatch(columnsSource, /groups\.members\.add|groups\.members\.remove/);
  });

  it("shows move OU in detail operations dropdown when permission is granted", () => {
    const detailSource = readFileSync(
      new URL("./AdGroupDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /AdManagement\.Groups\.MoveOu/);
    assert.match(detailSource, /buildAdGroupMoveOuPath/);
    assert.match(detailSource, /groups\.actions\.moveOu/);
    assert.doesNotMatch(detailSource, /groups\.members\.add|groups\.members\.remove/);
  });

  it("protects move OU route with Groups.MoveOu permission", () => {
    const routerSource = readFileSync(
      new URL("../../app/router.tsx", import.meta.url),
      "utf8",
    );

    assert.match(routerSource, /path: "\/ad-management\/groups\/:id\/move-ou"/);
    assert.match(routerSource, /RequirePermission permission=\{PermissionCodes\.AdManagement\.Groups\.MoveOu\}/);
    assert.match(routerSource, /AdMoveGroupOuPage/);
  });
});

describe("group move OU API", () => {
  it("posts to move-ou endpoint", () => {
    const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");

    assert.match(apiSource, /moveAdGroupOu/);
    assert.match(apiSource, /\/ad-management\/groups\/\$\{groupId\}\/move-ou/);
    assert.match(apiSource, /invalidateAdGroupOuMoveQueries/);
    assert.match(apiSource, /AD_OPERATION_LOGS_QUERY_KEY/);
  });
});

describe("group move OU return navigation", () => {
  it("builds detail path for post-move redirect", () => {
    assert.equal(buildAdGroupDetailPath(groupId), `/ad-management/groups/${groupId}`);
  });
});
