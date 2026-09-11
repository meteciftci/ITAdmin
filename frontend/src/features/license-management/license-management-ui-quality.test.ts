import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";

const featureRoot = dirname(fileURLToPath(import.meta.url));

function source(path: string): string {
  return readFileSync(join(featureRoot, path), "utf8");
}

test("seat management is rendered only for named-user packages", () => {
  assert.match(
    source("LicensePackageDetailPage.tsx"),
    /pkg && pkg\.licenseType === "NamedUser"/,
  );
});

test("assignment picker excludes incompatible and full packages", () => {
  const dialog = source("components/LicenseAssignmentDialog.tsx");

  assert.match(dialog, /item\.licenseType === "NamedUser"/);
  assert.match(dialog, /item\.availableQuantity > 0/);
});

test("fulfillment candidate failures are not presented as an empty result", () => {
  const page = source("LicenseFulfillmentPage.tsx");

  assert.match(page, /candidatesQuery\.isError \? \(/);
  assert.match(page, /!candidatesQuery\.isError && filteredCandidates\.length === 0/);
});

test("core license editors use native form submission", () => {
  for (const file of [
    "components/LicenseCompanyForm.tsx",
    "components/LicenseProductCategoryForm.tsx",
    "components/LicenseProductForm.tsx",
    "components/LicensePurchaseForm.tsx",
    "components/LicensePackageForm.tsx",
    "components/LicenseRequestForm.tsx",
  ]) {
    const contents = source(file);
    assert.match(contents, /<form[\s>]/, file);
    assert.match(contents, /onSubmit=/, file);
    assert.match(contents, /type="submit"/, file);
  }
});

test("license form labels are explicitly associated with their controls", () => {
  for (const file of [
    "components/FulfillmentPackageDefaultsForm.tsx",
    "components/FulfillmentTargetForm.tsx",
    "components/LicenseCompanyForm.tsx",
    "components/LicenseOuPicker.tsx",
    "components/LicensePackageForm.tsx",
    "components/LicenseProductCategoryForm.tsx",
    "components/LicenseProductForm.tsx",
    "components/LicensePurchaseForm.tsx",
    "components/LicenseRequestForm.tsx",
    "components/ManualLinesSection.tsx",
    "components/RenewalLinesSection.tsx",
  ]) {
    assert.doesNotMatch(source(file), /<Label>/, file);
  }
});

test("license keys are masked by default in the package editor", () => {
  const form = source("components/LicensePackageForm.tsx");

  assert.match(form, /type=\{showLicenseKey \? "text" : "password"\}/);
  assert.match(form, /aria-pressed=\{showLicenseKey\}/);
});

test("editor option query failures are visible and disable dependent controls", () => {
  for (const [file, query] of [
    ["components/LicenseProductForm.tsx", "categoriesQuery"],
    ["components/LicensePackageForm.tsx", "purchasesQuery"],
    ["components/LicensePurchaseForm.tsx", "companiesQuery"],
    ["components/LicenseRequestForm.tsx", "productsQuery"],
  ]) {
    const contents = source(file);
    assert.match(contents, new RegExp(`${query}\\.isError`), file);
    assert.match(contents, new RegExp(`${query}\\.isLoading \\|\\| ${query}\\.isError`), file);
  }
});
