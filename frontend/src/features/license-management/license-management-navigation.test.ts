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
    "/license-management/products",
    "/license-management/acquisitions",
    "/license-management/packages",
  ];
  for (const path of paths) {
    assert.match(routerSource, new RegExp(`path: "${path.replace(/\//g, "\\/")}"`));
  }
  assert.match(routerSource, /PermissionCodes\.LicenseManagement\.View/);
});

test("sidebar includes license management menu", () => {
  const sidebarSource = readFileSync(join(root, "components/layout/sidebar-items.ts"), "utf8");
  assert.match(sidebarSource, /routePrefix: "\/license-management"/);
  assert.match(sidebarSource, /items\.licenseManagementOverview/);
  assert.match(sidebarSource, /items\.licenseManagementCompanies/);
  assert.match(sidebarSource, /PermissionCodes\.LicenseManagement\.View/);
});

test("license management locale keys exist in tr and en", () => {
  const tr = JSON.parse(
    readFileSync(join(root, "locales/tr/licenseManagement.json"), "utf8"),
  ) as { licenseManagement: Record<string, unknown> };
  const en = JSON.parse(
    readFileSync(join(root, "locales/en/licenseManagement.json"), "utf8"),
  ) as { licenseManagement: Record<string, unknown> };

  assert.equal(typeof tr.licenseManagement.title, "string");
  assert.equal(typeof en.licenseManagement.title, "string");
  assert.equal(tr.licenseManagement.title, "Lisans Yönetimi");
  assert.equal(en.licenseManagement.title, "License Management");
});
