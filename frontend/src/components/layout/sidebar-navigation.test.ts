import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const sidebarSource = readFileSync(
  new URL("./sidebar-items.ts", import.meta.url),
  "utf8",
);

const systemGroupStart = sidebarSource.indexOf('labelKey: "groups.system"');
const systemGroupSource = sidebarSource.slice(systemGroupStart);

describe("sidebar navigation consolidation", () => {
  it("does not define an administration group", () => {
    assert.doesNotMatch(sidebarSource, /groups\.administration/);
  });

  it("places users, roles, and permissions links under the system group", () => {
    assert.match(systemGroupSource, /to: "\/users"/);
    assert.match(systemGroupSource, /to: "\/roles"/);
    assert.match(systemGroupSource, /to: "\/permissions"/);
    assert.match(systemGroupSource, /Users\.View/);
    assert.match(systemGroupSource, /Roles\.View/);
    assert.match(systemGroupSource, /Permissions\.View/);
  });

  it("keeps settings collapsible under the system group", () => {
    assert.match(systemGroupSource, /items\.settings/);
    assert.match(systemGroupSource, /routePrefix: "\/settings"/);
  });

  it("defines users, roles, and permissions only once in sidebar items", () => {
    assert.equal((sidebarSource.match(/to: "\/users"/g) ?? []).length, 1);
    assert.equal((sidebarSource.match(/to: "\/roles"/g) ?? []).length, 1);
    assert.equal((sidebarSource.match(/to: "\/permissions"/g) ?? []).length, 1);
  });
});

describe("assign roles dialog SuperAdmin handling", () => {
  it("does not hard-disable SuperAdmin role removal in the UI", () => {
    const dialogSource = readFileSync(
      new URL("../../features/users/AssignRolesDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.doesNotMatch(dialogSource, /cannot remove your own SuperAdmin role/i);
    assert.doesNotMatch(dialogSource, /role\.code\s*===\s*["']SuperAdmin["']/);
    assert.doesNotMatch(dialogSource, /disabled=\{[^}]*SuperAdmin/i);
  });

  it("maps last active SuperAdmin backend error to i18n key", () => {
    const dialogSource = readFileSync(
      new URL("../../features/users/AssignRolesDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(dialogSource, /assignRoles\.errors\.lastActiveSuperAdmin/);
    assert.match(
      dialogSource,
      /The last active SuperAdmin user cannot lose the SuperAdmin role\./,
    );
  });
});
