import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  isSelfRoleTarget,
  normalizeUserId,
} from "../auth/self-role-target.ts";

const reloginModuleSource = readFileSync(
  new URL("../auth/self-role-change-relogin.ts", import.meta.url),
  "utf8",
);

const assignRolesDialogSource = readFileSync(
  new URL("./AssignRolesDialog.tsx", import.meta.url),
  "utf8",
);

const loginPageSource = readFileSync(
  new URL("../auth/LoginPage.tsx", import.meta.url),
  "utf8",
);

describe("self role target detection", () => {
  it("normalizes user ids case-insensitively", () => {
    assert.equal(
      normalizeUserId("550E8400-E29B-41D4-A716-446655440000"),
      "550e8400-e29b-41d4-a716-446655440000",
    );
  });

  it("detects self role updates with different casing", () => {
    assert.equal(
      isSelfRoleTarget(
        "550E8400-E29B-41D4-A716-446655440000",
        "550e8400-e29b-41d4-a716-446655440000",
      ),
      true,
    );
    assert.equal(
      isSelfRoleTarget(
        "550e8400-e29b-41d4-a716-446655440000",
        "11111111-1111-1111-1111-111111111111",
      ),
      false,
    );
  });
});

describe("AssignRolesDialog self role relogin wiring", () => {
  it("does not refresh session after self role update", () => {
    assert.doesNotMatch(assignRolesDialogSource, /syncAuthenticatedUserSession/);
    assert.doesNotMatch(assignRolesDialogSource, /setUser\(/);
  });

  it("enforces relogin only for self role updates", () => {
    assert.match(assignRolesDialogSource, /isSelfRoleTarget/);
    assert.match(assignRolesDialogSource, /enforceReloginAfterSelfRoleChange/);
    assert.match(assignRolesDialogSource, /onUpdated\(\)/);
  });

  it("keeps logout and redirect out of the last active SuperAdmin error path", () => {
    const errorHandlerStart = assignRolesDialogSource.indexOf("onError:");
    const errorHandlerSource = assignRolesDialogSource.slice(errorHandlerStart);

    assert.doesNotMatch(errorHandlerSource, /enforceReloginAfterSelfRoleChange/);
    assert.doesNotMatch(errorHandlerSource, /navigate\(\s*["']\/login["']/);
  });

  it("uses permissions changed login route reason", () => {
    assert.match(reloginModuleSource, /reason:\s*LOGIN_PERMISSIONS_CHANGED_REASON/);
    assert.match(reloginModuleSource, /permissionsChanged/);
  });

  it("clears auth even when logout API fails", () => {
    assert.match(reloginModuleSource, /await logout\(\)/);
    assert.match(reloginModuleSource, /catch\s*\{/);
    assert.match(reloginModuleSource, /clearAuth\(\)/);
    assert.match(reloginModuleSource, /navigate\(\s*["']\/login["']/);
  });
});

describe("LoginPage permissions changed notice", () => {
  it("shows relogin message from route state", () => {
    assert.match(loginPageSource, /permissionsChanged/);
    assert.match(loginPageSource, /auth:permissionsChanged\.reloginMessage/);
  });
});
