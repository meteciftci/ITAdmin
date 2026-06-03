import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { buildAdUserDetailPath } from "./ad-user-detail-path.ts";
import type { AdUserListItem } from "./types.ts";

const sampleUser: AdUserListItem = {
  id: "550e8400-e29b-41d4-a716-446655440000",
  distinguishedName: "CN=User,DC=example,DC=com",
  samAccountName: "user.one",
  userPrincipalName: "user.one@example.com",
  displayName: "User One",
  mail: "user.one@example.com",
  department: "IT",
  isEnabled: true,
  isLockedOut: false,
  whenCreated: null,
  whenChanged: null,
  lastLogonAt: null,
};

describe("AD users list detail navigation", () => {
  it("builds detail route from list item id", () => {
    assert.equal(
      buildAdUserDetailPath(sampleUser.id),
      "/ad-management/users/550e8400-e29b-41d4-a716-446655440000",
    );
  });
});
