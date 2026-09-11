import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");

test("changing request currency does not rehydrate and reset the entire form", () => {
  const source = readFileSync(
    join(root, "features/license-management/components/LicenseRequestForm.tsx"),
    "utf8",
  );

  assert.doesNotMatch(
    source,
    /\}, \[currency, request, settingsQuery\.data\?\.defaultCurrency/,
  );
});

test("fulfillment selection disables request items that are not fulfillable", () => {
  const source = readFileSync(
    join(root, "features/license-management/LicenseFulfillmentPage.tsx"),
    "utf8",
  );

  assert.match(source, /disabled=\{isBusy \|\| !candidate\.isFulfillable\}/);
});

test("purchase edit persists the status selected in the form", () => {
  const source = readFileSync(
    join(root, "features/license-management/types.ts"),
    "utf8",
  );

  assert.match(source, /status:\s*LicensePurchaseStatus;/);
});

test("package edit persists the status selected in the form", () => {
  const source = readFileSync(
    join(root, "features/license-management/types.ts"),
    "utf8",
  );

  assert.match(source, /status:\s*LicensePackageStatus;/);
});

test("license option loaders are not silently capped at one hundred records", () => {
  const source = readFileSync(
    join(root, "features/license-management/api.ts"),
    "utf8",
  );

  assert.doesNotMatch(source, /pageSize: 100/);
});
