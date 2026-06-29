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
    "/license-management/requests",
    "/license-management/requests/create",
    "/license-management/requests/:id",
    "/license-management/requests/:id/edit",
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
  assert.match(sidebarSource, /items\.licenseManagementRequests/);
  assert.match(sidebarSource, /\/license-management\/purchases/);
  assert.match(sidebarSource, /\/license-management\/requests/);
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
  const requestsPage = readFileSync(
    join(root, "features/license-management/LicenseRequestsPage.tsx"),
    "utf8",
  );
  assert.match(requestsPage, /LICENSE_REQUEST_CREATE_PATH/);
  assert.doesNotMatch(requestsPage, /FormDialog/);
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

test("PopoverTrigger defaults to inline width; fullWidth is opt-in", () => {
  const popoverSource = readFileSync(join(root, "components/ui/popover.tsx"), "utf8");
  assert.match(popoverSource, /fullWidth\?: boolean/);
  assert.match(popoverSource, /fullWidth \? "flex w-full" : "inline-flex"/);
  assert.match(popoverSource, /fullWidth = false/);
});

test("DatePicker uses fullWidth PopoverTrigger with month/year dropdowns", () => {
  const datePickerSource = readFileSync(join(root, "components/common/DatePicker.tsx"), "utf8");
  assert.match(datePickerSource, /PopoverTrigger asChild fullWidth/);
  assert.match(datePickerSource, /captionLayout="dropdown"/);
  assert.match(datePickerSource, /navLayout="around"/);
  assert.match(datePickerSource, /startMonth=\{CALENDAR_START_MONTH\}/);
  assert.match(datePickerSource, /endMonth=\{CALENDAR_END_MONTH\}/);
  assert.match(datePickerSource, /datePicker\.today/);
  assert.match(datePickerSource, /onChange\(null\)/);
});

test("DataTable filter button does not use fullWidth PopoverTrigger", () => {
  const dataTableSource = readFileSync(join(root, "components/common/data-table.tsx"), "utf8");
  assert.match(dataTableSource, /PopoverTrigger asChild/);
  assert.doesNotMatch(dataTableSource, /PopoverTrigger asChild fullWidth/);
});

test("purchase detail shows linked license packages section", () => {
  const detailPage = readFileSync(
    join(root, "features/license-management/LicensePurchaseDetailPage.tsx"),
    "utf8",
  );
  const packagesSection = readFileSync(
    join(root, "features/license-management/components/LicensePurchasePackagesSection.tsx"),
    "utf8",
  );
  assert.match(detailPage, /LicensePurchasePackagesSection/);
  assert.match(detailPage, /isPurchaseFieldVisible/);
  assert.match(packagesSection, /linkedPackagesTitle/);
  assert.match(packagesSection, /linkedPackagesEmpty/);
  assert.match(packagesSection, /buildLicensePackageCreatePath\(purchaseId\)/);
  assert.match(packagesSection, /buildLicensePackagesListPath\(purchaseId\)/);
  assert.match(packagesSection, /showPurchaseColumn: false/);
});

test("package create and list pages support purchaseId query param", () => {
  const createPage = readFileSync(
    join(root, "features/license-management/LicensePackageCreatePage.tsx"),
    "utf8",
  );
  const listPage = readFileSync(
    join(root, "features/license-management/LicensePackagesPage.tsx"),
    "utf8",
  );
  assert.match(createPage, /searchParams\.get\("purchaseId"\)/);
  assert.match(createPage, /initialPurchaseId/);
  assert.match(createPage, /buildLicensePurchaseDetailPath\(initialPurchaseId\)/);
  assert.match(listPage, /useSearchParams/);
  assert.match(listPage, /searchParams\.get\("purchaseId"\)/);
  assert.match(listPage, /setSearchParams/);
  assert.match(listPage, /buildLicensePackageCreatePath\(purchaseIdFilter/);
});

test("purchase detail hides type-specific fields via shared helper", () => {
  const detailPage = readFileSync(
    join(root, "features/license-management/LicensePurchaseDetailPage.tsx"),
    "utf8",
  );
  const fieldsHelper = readFileSync(
    join(root, "features/license-management/purchase-form-fields.ts"),
    "utf8",
  );
  assert.match(detailPage, /showField\(purchase\.purchaseType, "tenderNumber"\)/);
  assert.match(detailPage, /showField\(purchase\.purchaseType, "directPurchaseNumber"\)/);
  assert.match(detailPage, /showField\(purchase\.purchaseType, "dmoOrderNumber"\)/);
  assert.match(fieldsHelper, /DirectPurchase/);
  assert.match(fieldsHelper, /Tender/);
  assert.match(fieldsHelper, /LegacyPerpetual/);
});

test("license management locale includes linked packages and date picker keys", () => {
  const trCommon = JSON.parse(readFileSync(join(root, "locales/tr/common.json"), "utf8")) as {
    common: { datePicker: Record<string, string> };
  };
  const enCommon = JSON.parse(readFileSync(join(root, "locales/en/common.json"), "utf8")) as {
    common: { datePicker: Record<string, string> };
  };
  const tr = JSON.parse(
    readFileSync(join(root, "locales/tr/licenseManagement.json"), "utf8"),
  ) as {
    licenseManagement: { pages: { purchases: { detail: Record<string, string> } } };
  };
  const en = JSON.parse(
    readFileSync(join(root, "locales/en/licenseManagement.json"), "utf8"),
  ) as {
    licenseManagement: { pages: { purchases: { detail: Record<string, string> } } };
  };

  assert.equal(trCommon.common.datePicker.today, "Bugün");
  assert.equal(enCommon.common.datePicker.today, "Today");
  assert.equal(tr.licenseManagement.pages.purchases.detail.linkedPackagesTitle, "Bu Satın Almaya Bağlı Lisans Paketleri");
  assert.equal(
    en.licenseManagement.pages.purchases.detail.linkedPackagesTitle,
    "License Packages in This Purchase",
  );
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

test("license request pages use page routes and DatePicker", () => {
  const createPage = readFileSync(
    join(root, "features/license-management/LicenseRequestCreatePage.tsx"),
    "utf8",
  );
  const requestForm = readFileSync(
    join(root, "features/license-management/components/LicenseRequestForm.tsx"),
    "utf8",
  );
  const adGuard = readFileSync(
    join(root, "features/license-management/components/LicenseRequestAdAccessGuard.tsx"),
    "utf8",
  );

  assert.match(createPage, /LicenseRequestCreatePage/);
  assert.match(createPage, /LicenseRequestAdAccessGuard/);
  assert.doesNotMatch(createPage, /FormDialog/);
  assert.match(requestForm, /DatePicker/);
  assert.doesNotMatch(requestForm, /type="date"/);
  assert.match(adGuard, /BlockingStateCard/);
  assert.match(adGuard, /Directory\.Users\.Lookup/);
});

test("license request payload helpers enforce duplicate rules", () => {
  const validation = readFileSync(
    join(root, "features/license-management/license-request-form-validation.ts"),
    "utf8",
  );
  const payload = readFileSync(
    join(root, "features/license-management/license-request-payload.ts"),
    "utf8",
  );

  assert.match(validation, /duplicateProduct/);
  assert.match(validation, /duplicateUser/);
  assert.match(payload, /mapAdUserToSnapshot/);
  assert.match(payload, /buildLicenseRequestPayload/);
});
