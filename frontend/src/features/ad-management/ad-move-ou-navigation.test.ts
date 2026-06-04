import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { AD_USER_FORM_ACTIONS_CLASSNAME } from "./ad-form-actions.ts";
import {
  buildAdUserDetailReturnState,
  buildAdUsersListReturnState,
  resolveAdUserReturnPath,
  resolveAdUserReturnPathFromLocation,
} from "./ad-return-path.ts";
import { buildAdUserDetailPath } from "./ad-user-detail-path.ts";
import { AD_USERS_LIST_PATH } from "./ad-users-list-path.ts";
import { getSnapshotRenderStrategy } from "./parse-ad-operation-snapshot.ts";

const userId = "550e8400-e29b-41d4-a716-446655440000";
const listPath = AD_USERS_LIST_PATH;
const detailPath = buildAdUserDetailPath(userId);

describe("AD move OU navigation", () => {
  it("list return state targets users list path", () => {
    assert.deepEqual(buildAdUsersListReturnState(), {
      returnTo: listPath,
      returnLabel: "adUsersList",
    });
    assert.equal(resolveAdUserReturnPath(buildAdUsersListReturnState()), listPath);
  });

  it("detail return state targets user detail path", () => {
    const state = buildAdUserDetailReturnState(userId);

    assert.equal(state.returnTo, detailPath);
    assert.equal(resolveAdUserReturnPath(state), detailPath);
  });

  it("uses list fallback when return state is missing", () => {
    assert.equal(resolveAdUserReturnPathFromLocation(null, undefined, listPath), listPath);
    assert.equal(resolveAdUserReturnPathFromLocation(undefined, undefined, listPath), listPath);
  });

  it("uses UserOuMove dedicated snapshot strategy", () => {
    assert.equal(getSnapshotRenderStrategy("UserOuMove"), "ouMove");
    assert.notEqual(getSnapshotRenderStrategy("UserOuMove"), "generic");
  });
});

describe("AdMoveUserOuPage form actions", () => {
  it("aligns actions to the end like edit forms", () => {
    assert.equal(AD_USER_FORM_ACTIONS_CLASSNAME, "flex flex-wrap justify-end gap-2");
  });
});
