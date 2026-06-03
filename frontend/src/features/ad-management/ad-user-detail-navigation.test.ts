import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { buildAdUserDetailReturnState } from "./ad-return-path.ts";
import { buildAdUserDetailPath } from "./ad-user-detail-path.ts";

const userId = "550e8400-e29b-41d4-a716-446655440000";

describe("AD user detail navigation state", () => {
  it("builds detail route and matching return state for edit/groups", () => {
    const detailPath = buildAdUserDetailPath(userId);
    assert.equal(detailPath, `/ad-management/users/${userId}`);
    assert.deepEqual(buildAdUserDetailReturnState(userId), {
      returnTo: detailPath,
      returnLabel: "adUserDetail",
    });
  });
});
