import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { groupPermissionsByModule } from "./permission-catalog.ts";

const permission = (id: string, module: string, code: string) => ({
  id,
  module,
  name: code.replaceAll(".", " "),
  code,
  description: null,
  isActive: true,
});

describe("groupPermissionsByModule", () => {
  it("uses backend module metadata without deriving groups from permission codes", () => {
    const groups = groupPermissionsByModule([
      permission("1", "Identity", "Users.View"),
      permission("2", "Identity", "Roles.View"),
      permission("3", "Operations", "Users.Export"),
    ]);

    assert.deepEqual(groups.map((group) => group.module), ["Identity", "Operations"]);
    assert.deepEqual(groups[0]?.items.map((item) => item.id), ["1", "2"]);
    assert.deepEqual(groups[1]?.items.map((item) => item.id), ["3"]);
  });
});
