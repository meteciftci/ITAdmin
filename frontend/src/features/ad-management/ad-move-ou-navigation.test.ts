import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { buildAdUserDetailReturnState, resolveAdUserReturnPath } from "./ad-return-path.ts";
import { buildAdUserDetailPath } from "./ad-user-detail-path.ts";

describe("AD move OU navigation", () => {
  const userId = "550e8400-e29b-41d4-a716-446655440000";

  it("builds detail return state for move OU route", () => {
    const state = buildAdUserDetailReturnState(userId);

    assert.equal(state.returnTo, buildAdUserDetailPath(userId));
    assert.equal(state.returnLabel, "adUserDetail");
  });

  it("resolves return path from detail navigation state", () => {
    const returnPath = resolveAdUserReturnPath(buildAdUserDetailReturnState(userId));

    assert.equal(returnPath, `/ad-management/users/${userId}`);
  });
});
