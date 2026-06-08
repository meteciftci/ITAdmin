import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("AdDeleteGroupConfirmDialog", () => {
  it("requires typed sAMAccountName confirmation and uses destructive submit", () => {
    const source = readFileSync(
      new URL("./components/AdDeleteGroupConfirmDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /groups\.delete\.description/);
    assert.match(source, /groups\.delete\.confirmLabel/);
    assert.match(source, /groups\.delete\.confirmButton/);
    assert.match(source, /resolveConfirmationValue/);
    assert.match(source, /toLowerCase\(\)/);
    assert.match(source, /disabled=\{!group \|\| !isConfirmMatch/);
    assert.match(source, /variant="destructive"/);
    assert.match(source, /deleteAdGroup/);
    assert.match(source, /getApiErrorMessage/);
    assert.match(source, /invalidateAdManagementGroupQueries/);
    assert.match(source, /AD_OPERATION_LOGS_QUERY_KEY/);
    assert.doesNotMatch(source, /manageGroups|moveOu|restore/i);
  });
});
