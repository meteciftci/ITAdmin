import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const dialogSource = readFileSync(new URL("./dialog.tsx", import.meta.url), "utf8");
const addMemberDialogSource = readFileSync(
  new URL("../../features/ad-management/components/AdAddGroupMemberDialog.tsx", import.meta.url),
  "utf8",
);
const attributeMappingDialogSource = readFileSync(
  new URL("../../features/ad-management/components/AdAttributeMappingDialog.tsx", import.meta.url),
  "utf8",
);

describe("DialogBody", () => {
  it("exports DialogBody from dialog module", () => {
    assert.match(dialogSource, /function DialogBody/);
    assert.match(dialogSource, /export \{[\s\S]*DialogBody/);
  });

  it("applies default body spacing classes", () => {
    assert.match(dialogSource, /data-slot="dialog-body"/);
    assert.match(dialogSource, /"space-y-4 px-4 py-4"/);
  });

  it("is used in AdAddGroupMemberDialog without bare space-y-4 body wrapper", () => {
    assert.match(addMemberDialogSource, /DialogBody/);
    assert.doesNotMatch(
      addMemberDialogSource,
      /<DialogHeader>[\s\S]*?<div className="space-y-4">/,
    );
  });

  it("does not leave double-padding body wrappers in migrated dialogs", () => {
    assert.doesNotMatch(addMemberDialogSource, /DialogBody className="[^"]*p-4/);
    assert.doesNotMatch(attributeMappingDialogSource, /space-y-4 p-4/);
    assert.match(attributeMappingDialogSource, /<DialogBody>/);
  });
});
