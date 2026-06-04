import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildAdUserDetailReturnState,
  buildAdUsersListReturnState,
  readAdUserReturnToFromState,
  resolveAdUserReturnPath,
  resolveAdUserReturnPathFromLocation,
  resolveSafeReturnPath,
} from "./ad-return-path.ts";
import { buildAdUserDetailPath } from "./ad-user-detail-path.ts";

const userId = "550e8400-e29b-41d4-a716-446655440000";
const detailPath = buildAdUserDetailPath(userId);
const listPath = "/ad-management/users";

describe("buildAdUsersListReturnState", () => {
  it("returns list path and label", () => {
    assert.deepEqual(buildAdUsersListReturnState(), {
      returnTo: listPath,
      returnLabel: "adUsersList",
    });
  });
});

describe("buildAdUserDetailReturnState", () => {
  it("returns detail path and label", () => {
    assert.deepEqual(buildAdUserDetailReturnState(userId), {
      returnTo: detailPath,
      returnLabel: "adUserDetail",
    });
  });
});

describe("resolveAdUserReturnPath", () => {
  it("uses returnTo from location state when safe", () => {
    assert.equal(
      resolveAdUserReturnPath({ returnTo: detailPath, returnLabel: "adUserDetail" }),
      detailPath,
    );
  });

  it("falls back to list when state is missing", () => {
    assert.equal(resolveAdUserReturnPath(null), listPath);
    assert.equal(resolveAdUserReturnPath(undefined), listPath);
    assert.equal(resolveAdUserReturnPath({}), listPath);
  });

  it("rejects unsafe returnTo values", () => {
    assert.equal(resolveAdUserReturnPath({ returnTo: "https://evil.example" }), listPath);
    assert.equal(resolveAdUserReturnPath({ returnTo: "javascript:alert(1)" }), listPath);
    assert.equal(resolveAdUserReturnPath({ returnTo: "//evil.example" }), listPath);
  });
});

describe("resolveAdUserReturnPathFromLocation", () => {
  it("prefers state over legacy query returnTo", () => {
    const searchParams = new URLSearchParams({
      returnTo: encodeURIComponent(listPath),
    });
    assert.equal(
      resolveAdUserReturnPathFromLocation(
        { returnTo: detailPath },
        searchParams,
        listPath,
      ),
      detailPath,
    );
  });

  it("defaults to list fallback when state and query are absent", () => {
    assert.equal(resolveAdUserReturnPathFromLocation(null, undefined, listPath), listPath);
    assert.equal(resolveAdUserReturnPathFromLocation(undefined, undefined, listPath), listPath);
  });

  it("supports legacy query returnTo when state is absent", () => {
    const searchParams = new URLSearchParams({
      returnTo: encodeURIComponent(detailPath),
    });
    assert.equal(
      resolveAdUserReturnPathFromLocation(null, searchParams, listPath),
      detailPath,
    );
  });
});

describe("readAdUserReturnToFromState", () => {
  it("reads returnTo only from object state", () => {
    assert.equal(readAdUserReturnToFromState({ returnTo: detailPath }), detailPath);
    assert.equal(readAdUserReturnToFromState("invalid"), undefined);
  });
});

describe("resolveSafeReturnPath", () => {
  it("allows internal paths", () => {
    assert.equal(resolveSafeReturnPath(detailPath), detailPath);
  });
});
