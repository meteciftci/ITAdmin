import { expect, test, type Page } from "@playwright/test";

const allPermissions = [
  "AdManagement.Users.View",
  "AdManagement.Users.Create",
  "AdManagement.Users.Update",
  "AdManagement.Users.Enable",
  "AdManagement.Users.Disable",
  "AdManagement.Users.Unlock",
  "AdManagement.Users.Groups.View",
  "AdManagement.Users.MoveOu",
  "LicenseManagement.View",
  "LicenseManagement.ManageCatalog",
  "LicenseManagement.ManagePurchases",
  "LicenseManagement.ManagePackages",
  "LicenseManagement.ManageRequests",
  "LicenseManagement.FulfillRequests",
  "LicenseManagement.ManageAssignments",
];

async function mockApplicationApi(page: Page) {
  await page.route("**/*", async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (!path.startsWith("/api/")) {
      await route.continue();
      return;
    }

    if (path === "/api/auth/me") {
      await route.fulfill({
        json: {
          userId: "00000000-0000-0000-0000-000000000001",
          userName: "phase9-admin",
          displayName: "Phase 9 Admin",
          email: "admin@example.com",
          roles: ["Administrator"],
          permissions: allPermissions,
          isSuperAdmin: false,
          preferredLanguage: "en",
        },
      });
      return;
    }

    if (path === "/api/auth/session-options") {
      await route.fulfill({
        json: {
          rememberMeEnabled: true,
          idleTimeoutMinutes: 60,
          idleWarningSeconds: 60,
          accessTokenMinutes: 30,
        },
      });
      return;
    }

    if (path === "/api/settings/branding") {
      await route.fulfill({
        json: {
          applicationName: "ITAdmin",
          browserTitle: "ITAdmin",
          logoUrl: null,
          faviconUrl: "/favicon.svg",
          forgotPasswordUrl: null,
          footerText: "ITAdmin",
        },
      });
      return;
    }

    if (path === "/api/ad-management/settings") {
      await route.fulfill({
        json: {
          isConfigured: true,
          isEnabled: true,
          notificationSettings: { rules: [] },
        },
      });
      return;
    }

    if (path === "/api/ad-management/users") {
      await route.fulfill({
        json: {
          items: [
            {
              id: "11111111-1111-1111-1111-111111111111",
              distinguishedName: "CN=Ada Lovelace,OU=Users,DC=example,DC=com",
              samAccountName: "ada.lovelace",
              userPrincipalName: "ada.lovelace@example.com",
              displayName: "Ada Lovelace",
              mail: "ada.lovelace@example.com",
              department: "Engineering",
              isEnabled: true,
              isLockedOut: false,
              whenCreated: "2026-01-01T00:00:00Z",
              whenChanged: "2026-01-01T00:00:00Z",
              lastLogonAt: "2026-09-10T08:00:00Z",
            },
          ],
          pageNumber: 1,
          pageSize: 20,
          hasNextPage: false,
        },
      });
      return;
    }

    if (path === "/api/license-management/products") {
      await route.fulfill({
        json: {
          items: [
            {
              id: "22222222-2222-2222-2222-222222222222",
              name: "Visual Studio",
              brand: "Microsoft",
              categoryId: "33333333-3333-3333-3333-333333333333",
              categoryName: "Developer Tools",
              isActive: true,
            },
          ],
          pageNumber: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        },
      });
      return;
    }

    if (path === "/api/license-management/requests") {
      await route.fulfill({
        json: {
          items: [
            {
              id: "44444444-4444-4444-4444-444444444444",
              requestSource: "CorporateRequestSystem",
              requestDate: "2026-09-11",
              externalRequestNumber: "REQ-PHASE9",
              ebysNumber: null,
              requesterUnitDisplayName: "Engineering",
              requesterManagerName: "Grace Hopper",
              productCount: 1,
              userCount: 1,
              requestedQuantity: 1,
              estimatedTotalCost: 500,
              currency: "TRY",
              status: "Approved",
            },
          ],
          pageNumber: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        },
      });
      return;
    }

    await route.fulfill({ status: 404, json: { message: `Unmocked API path: ${path}` } });
  });
}

test.beforeEach(async ({ page }) => {
  await mockApplicationApi(page);
});

test("AD user list passes authentication, module readiness, search and table rendering", async ({ page }) => {
  await page.goto("/ad-management/users");
  const search = page.getByPlaceholder(/Search users|Kullanıcı ara/i);
  await expect(search).toBeVisible();
  await search.fill("Ada");
  await expect(page.getByText("Ada Lovelace", { exact: true })).toBeVisible();
  await expect(page.getByText("ada.lovelace@example.com", { exact: true }).first()).toBeVisible();
});

test("license request list passes authentication, permission and server data rendering", async ({ page }) => {
  await page.goto("/license-management/requests");
  await expect(page.getByText(/REQ-PHASE9/).first()).toBeVisible();
  await expect(page.getByText("Engineering", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("link", { name: /Create request|Talep oluştur/i })).toBeVisible();
});
