import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("AdUserGroupsSummarySection", () => {
  it("does not render manage groups action in summary section", () => {
    const source = readFileSync(
      new URL("./components/ad-user-detail/AdUserGroupsSummarySection.tsx", import.meta.url),
      "utf8",
    );

    assert.equal(source.includes("manageGroups"), false);
    assert.equal(source.includes("users.actions.manageGroups"), false);
  });
});
