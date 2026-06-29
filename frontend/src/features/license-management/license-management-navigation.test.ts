import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");

test("router includes license management routes", () => {
  const routerSource = readFileSync(join(root, "app/router.tsx"), "utf8");
  const paths = [
    "/license-management/overview",
    "/license-management/companies",
    "/license-management/companies/create",
    "/license-management/companies/:id",
    "/license-management/companies/:id/edit",
    "/license-management/products",
    "/license-management/products/create",
    "/license-management/products/:id",
    "/license-management/products/:id/edit",
    "/license-management/purchases",
    "/license-management/purchases/create",
    "/license-management/purchases/:id",
    "/license-management/purchases/:id/edit",
    "/license-management/packages",
    "/license-management/packages/create",
    "/license-management/packages/:id",
    "/license-management/packages/:id/edit",
    "/settings/modules/license-management",
  ];
  for (const path of paths) {
    assert.match(routerSource, new RegExp(`path: "${path.replace(/\//g, "\\/")}"`));
  }
  assert.match(routerSource, /PermissionCodes\.LicenseManagement\.View/);
  assert.match(routerSource, /Navigate to="\/license-management\/purchases"/);
  assert.doesNotMatch(routerSource, /LicenseAcquisitionsPage/);
});

test("sidebar includes license management purchases menu", () => {
  const sidebarSource = readFileSync(join(root, "components/layout/sidebar-items.ts"), "utf8");
  assert.match(sidebarSource, /routePrefix: "\/license-management"/);
  assert.match(sidebarSource, /items\.licenseManagementOverview/);
  assert.match(sidebarSource, /items\.licenseManagementCompanies/);
  assert.match(sidebarSource, /items\.licenseManagementPurchases/);
  assert.match(sidebarSource, /\/license-management\/purchases/);
  assert.doesNotMatch(sidebarSource, /licenseManagementAcquisitions/);
  assert.doesNotMatch(sidebarSource, /\/license-management\/settings/);
  assert.match(sidebarSource, /PermissionCodes\.LicenseManagement\.View/);
});

test("module settings hub includes license management card", () => {
  const moduleSettingsSource = readFileSync(
    join(root, "features/settings/ModuleSettingsPage.tsx"),
    "utf8",
  );
  assert.match(moduleSettingsSource, /\/settings\/modules\/license-management/);
  assert.match(moduleSettingsSource, /LicenseManagement\.ManageSettings/);
});

test("list pages navigate to create routes instead of dialogs", () => {
  const companiesPage = readFileSync(
    join(root, "features/license-management/LicenseCompaniesPage.tsx"),
    "utf8",
  );
  const purchasesPage = readFileSync(
    join(root, "features/license-management/LicensePurchasesPage.tsx"),
    "utf8",
  );
  assert.match(companiesPage, /LICENSE_COMPANY_CREATE_PATH/);
  assert.doesNotMatch(companiesPage, /FormDialog/);
  assert.match(purchasesPage, /LICENSE_PURCHASE_CREATE_PATH/);
  assert.doesNotMatch(purchasesPage, /FormDialog/);
});

test("purchase and package forms use DatePicker not type=date", () => {
  const purchaseForm = readFileSync(
    join(root, "features/license-management/components/LicensePurchaseForm.tsx"),
    "utf8",
  );
  const packageForm = readFileSync(
    join(root, "features/license-management/components/LicensePackageForm.tsx"),
    "utf8",
  );
  assert.match(purchaseForm, /DatePicker/);
  assert.match(packageForm, /DatePicker/);
  assert.doesNotMatch(purchaseForm, /type="date"/);
  assert.doesNotMatch(packageForm, /type="date"/);
});

test("DatePicker trigger uses full width popover wrapper", () => {
  const popoverSource = readFileSync(join(root, "components/ui/popover.tsx"), "utf8");
  assert.match(popoverSource, /className="flex w-full"/);
});

test("purchase form uses type-based field visibility and payload normalization", () => {
  const purchaseForm = readFileSync(
    join(root, "features/license-management/components/LicensePurchaseForm.tsx"),
    "utf8",
  );
  assert.match(purchaseForm, /isPurchaseFieldVisible/);
  assert.match(purchaseForm, /buildPurchasePayloadByType/);
  assert.match(purchaseForm, /form\.sections\.basic/);
  assert.doesNotMatch(purchaseForm, /AcquisitionFormDialog/);
});

test("license forms use license management api error helper", () => {
  const productForm = readFileSync(
    join(root, "features/license-management/components/LicenseProductForm.tsx"),
    "utf8",
  );
  const settingsPage = readFileSync(
    join(root, "features/settings/LicenseManagementSettingsPage.tsx"),
    "utf8",
  );
  assert.match(productForm, /getLicenseManagementApiErrorMessage/);
  assert.match(settingsPage, /getLicenseManagementApiErrorMessage/);
});

test("license management locale keys exist in tr and en without legacy terms", () => {
  const tr = JSON.parse(
    readFileSync(join(root, "locales/tr/licenseManagement.json"), "utf8"),
  ) as { licenseManagement: Record<string, unknown> };
  const en = JSON.parse(
    readFileSync(join(root, "locales/en/licenseManagement.json"), "utf8"),
  ) as { licenseManagement: Record<string, unknown> };
  const trNav = JSON.parse(readFileSync(join(root, "locales/tr/navigation.json"), "utf8")) as {
    navigation: { items: Record<string, string> };
  };
  const enNav = JSON.parse(readFileSync(join(root, "locales/en/navigation.json"), "utf8")) as {
    navigation: { items: Record<string, string> };
  };

  assert.equal(tr.licenseManagement.purchases && typeof tr.licenseManagement.purchases === "object", true);
  assert.equal(en.licenseManagement.purchases && typeof en.licenseManagement.purchases === "object", true);
  assert.equal(trNav.navigation.items.licenseManagementPurchases, "Satın Alımlar");
  assert.equal(enNav.navigation.items.licenseManagementPurchases, "Purchases");

  const trRaw = readFileSync(join(root, "locales/tr/licenseManagement.json"), "utf8");
  const enRaw = readFileSync(join(root, "locales/en/licenseManagement.json"), "utf8");
  assert.doesNotMatch(trRaw, /Edinim/i);
  assert.doesNotMatch(enRaw, /Acquisition/i);
});
