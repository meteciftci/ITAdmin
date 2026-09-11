import assert from "node:assert/strict";
import { test } from "node:test";

import type { CurrentUser } from "../auth/types.ts";
import { PermissionCodes } from "../../lib/permission-codes.ts";
import { canAccess } from "../../lib/permissions.ts";

function userWith(...permissions: string[]): CurrentUser {
  return {
    userId: "user-1",
    userName: "tester",
    displayName: "Test User",
    email: null,
    roles: [],
    permissions,
    isSuperAdmin: false,
    preferredLanguage: "en",
  };
}

test("scoped license permissions imply base module view", () => {
  const user = userWith(PermissionCodes.LicenseManagement.ManageRequests);

  assert.equal(canAccess(user, PermissionCodes.LicenseManagement.View), true);
  assert.equal(canAccess(user, PermissionCodes.LicenseManagement.ManagePurchases), false);
});

test("base license view does not imply sensitive package access", () => {
  const user = userWith(PermissionCodes.LicenseManagement.View);

  assert.equal(canAccess(user, PermissionCodes.LicenseManagement.ViewSensitiveData), false);
});

test("settings and sensitive-data permissions do not independently grant module view", () => {
  assert.equal(
    canAccess(userWith(PermissionCodes.LicenseManagement.ManageSettings), PermissionCodes.LicenseManagement.View),
    false,
  );
  assert.equal(
    canAccess(userWith(PermissionCodes.LicenseManagement.ViewSensitiveData), PermissionCodes.LicenseManagement.View),
    false,
  );
});
