import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import { buildAdOrganizationalUnitDetailPath } from "./ad-ou-detail-path.ts";
import { AD_ORGANIZATIONAL_UNITS_LIST_PATH } from "./ad-ous-list-path.ts";
import {
  AD_OPERATION_LOG_COVERAGE_OPERATION_TYPES,
  buildAdOperationLogCoverageMatrix,
} from "./ad-operation-log-coverage-matrix.ts";
import { getSnapshotRenderStrategy } from "./parse-ad-operation-snapshot.ts";

const organizationalUnitId = "550e8400-e29b-41d4-a716-446655440000";

function readLocaleOperations(locale: "tr" | "en"): Record<string, string> {
  const source = readFileSync(
    new URL(`../../locales/${locale}/adOperationLogs.json`, import.meta.url),
    "utf8",
  );
  const parsed = JSON.parse(source) as { adOperationLogs: { operations: Record<string, string> } };
  return parsed.adOperationLogs.operations;
}

describe("ad organizational units navigation", () => {
  it("builds organizational unit detail path", () => {
    assert.equal(
      buildAdOrganizationalUnitDetailPath(organizationalUnitId),
      `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/${organizationalUnitId}`,
    );
  });

  it("protects organizational units routes with AdManagement.OrganizationalUnits.View permission", () => {
    const routerSource = readFileSync(
      new URL("../../app/router.tsx", import.meta.url),
      "utf8",
    );

    assert.match(routerSource, /path: "\/ad-management\/organizational-units"/);
    assert.match(routerSource, /path: "\/ad-management\/organizational-units\/:id"/);
    assert.match(
      routerSource,
      /RequirePermission permission="AdManagement\.OrganizationalUnits\.View"/,
    );
    assert.match(routerSource, /AdOrganizationalUnitsPage/);
    assert.match(routerSource, /AdOrganizationalUnitDetailPage/);
  });

  it("shows organizational units menu item only for organizational units permission", () => {
    const sidebarSource = readFileSync(
      new URL("../../components/layout/sidebar-items.ts", import.meta.url),
      "utf8",
    );

    assert.match(sidebarSource, /AdManagement\.OrganizationalUnits\.View/);
    assert.match(sidebarSource, /to: "\/ad-management\/organizational-units"/);
    assert.match(sidebarSource, /items\.adManagementOrganizationalUnits/);
    assert.match(sidebarSource, /isAdManagementOrganizationalUnitsVisible/);
  });

  it("places organizational units menu item directly above deleted objects", () => {
    const sidebarSource = readFileSync(
      new URL("../../components/layout/sidebar-items.ts", import.meta.url),
      "utf8",
    );

    const organizationalUnitsIndex = sidebarSource.indexOf("items.adManagementOrganizationalUnits");
    const deletedObjectsIndex = sidebarSource.indexOf("items.adManagementDeletedObjects");

    assert.ok(organizationalUnitsIndex >= 0);
    assert.ok(deletedObjectsIndex >= 0);
    assert.ok(organizationalUnitsIndex < deletedObjectsIndex);
  });

  it("uses list toolbar, DataTable, pagination and OU dialogs", () => {
    const pageSource = readFileSync(
      new URL("./AdOrganizationalUnitsPage.tsx", import.meta.url),
      "utf8",
    );
    const toolbarSource = readFileSync(
      new URL("./components/AdOrganizationalUnitsSearchToolbar.tsx", import.meta.url),
      "utf8",
    );
    const columnsSource = readFileSync(
      new URL("./ad-ous-columns.tsx", import.meta.url),
      "utf8",
    );
    const dialogsSource = readFileSync(
      new URL("./components/AdOrganizationalUnitDialogs.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /organizationalUnits\.searchPlaceholder/);
    assert.match(toolbarSource, /listState/);
    assert.match(pageSource, /DataTable/);
    assert.match(pageSource, /DataTablePagination/);
    assert.match(columnsSource, /organizationalUnits\.table\./);
    assert.match(dialogsSource, /AdCreateOrganizationalUnitDialog/);
    assert.match(dialogsSource, /AdRenameOrganizationalUnitDialog/);
    assert.match(dialogsSource, /AdMoveOrganizationalUnitDialog/);
    assert.match(dialogsSource, /AdDeleteOrganizationalUnitDialog/);
    assert.match(dialogsSource, /AdOuSearchCombobox/);
  });

  it("detail page includes summary, content and child OU sections", () => {
    const detailSource = readFileSync(
      new URL("./AdOrganizationalUnitDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /organizationalUnits\.detail\.sections\.overview/);
    assert.match(detailSource, /organizationalUnits\.detail\.sections\.contentSummary/);
    assert.match(detailSource, /organizationalUnits\.detail\.sections\.childOrganizationalUnits/);
    assert.match(detailSource, /AdOrganizationalUnitRecentOperationsSection/);
  });

  it("uses correct API endpoints and query keys", () => {
    const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");

    assert.match(apiSource, /\/ad-management\/organizational-units\/manage/);
    assert.match(apiSource, /export const getAdOrganizationalUnits/);
    assert.match(apiSource, /export const getAdOrganizationalUnitById/);
    assert.match(apiSource, /export const createAdOrganizationalUnit/);
    assert.match(apiSource, /export const renameAdOrganizationalUnit/);
    assert.match(apiSource, /export const moveAdOrganizationalUnit/);
    assert.match(apiSource, /export const deleteAdOrganizationalUnit/);
    assert.match(apiSource, /AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY/);
    assert.match(apiSource, /invalidateAdOrganizationalUnitQueries/);
  });

  it("blocks invalid move targets in frontend DN helper", () => {
    const dnHelperSource = readFileSync(
      new URL("./ad-ldap-dn.ts", import.meta.url),
      "utf8",
    );

    assert.match(dnHelperSource, /isInvalidOrganizationalUnitMoveTarget/);
    assert.match(dnHelperSource, /isEqualOrDescendantOf/);
  });
});

describe("ad organizational units operation log coverage", () => {
  const trOperations = readLocaleOperations("tr");
  const enOperations = readLocaleOperations("en");
  const matrix = buildAdOperationLogCoverageMatrix(trOperations, enOperations);

  const ouOperationTypes = [
    "OrganizationalUnitCreate",
    "OrganizationalUnitRename",
    "OrganizationalUnitMove",
    "OrganizationalUnitDelete",
  ] as const;

  for (const operationType of ouOperationTypes) {
    it(`includes ${operationType} in coverage matrix and locale labels`, () => {
      assert.ok(AD_OPERATION_LOG_COVERAGE_OPERATION_TYPES.includes(operationType));
      const row = matrix.find((entry) => entry.operationType === operationType);
      assert.ok(row, `Missing coverage row for ${operationType}`);
      assert.equal(row.frontendLabelExists, true);
      assert.equal(row.trLocaleExists, true);
      assert.equal(row.enLocaleExists, true);
      assert.equal(row.snapshotRendererExists, true);
      assert.equal(getSnapshotRenderStrategy(operationType), "organizationalUnit");
    });
  }
});

describe("ad organizational units i18n", () => {
  it("defines organizationalUnits keys in TR and EN adManagement locales", () => {
    const trAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        organizationalUnits: {
          title: string;
          detail: { title: string; recentOperations: string };
          actions: { create: string; rename: string; move: string; delete: string };
        };
        apiMessages: {
          organizationalUnits: { notEmpty: string };
        };
      };
    };
    const enAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        organizationalUnits: {
          title: string;
          detail: { title: string; recentOperations: string };
          actions: { create: string; rename: string; move: string; delete: string };
        };
        apiMessages: {
          organizationalUnits: { notEmpty: string };
        };
      };
    };

    assert.equal(trAdManagement.adManagement.organizationalUnits.title, "Organizasyon Birimleri");
    assert.equal(
      trAdManagement.adManagement.organizationalUnits.detail.title,
      "OU Detayı",
    );
    assert.equal(
      trAdManagement.adManagement.organizationalUnits.detail.recentOperations,
      "Son Operasyonlar",
    );
    assert.equal(
      trAdManagement.adManagement.organizationalUnits.actions.create,
      "OU Oluştur",
    );
    assert.ok(trAdManagement.adManagement.apiMessages.organizationalUnits.notEmpty);

    assert.equal(enAdManagement.adManagement.organizationalUnits.title, "Organizational units");
    assert.equal(
      enAdManagement.adManagement.organizationalUnits.detail.title,
      "OU detail",
    );
    assert.equal(
      enAdManagement.adManagement.organizationalUnits.detail.recentOperations,
      "Recent operations",
    );
    assert.equal(
      enAdManagement.adManagement.organizationalUnits.actions.create,
      "Create OU",
    );
    assert.ok(enAdManagement.adManagement.apiMessages.organizationalUnits.notEmpty);
  });

  it("defines navigation keys for organizational units", () => {
    const trNavigation = JSON.parse(
      readFileSync(new URL("../../locales/tr/navigation.json", import.meta.url), "utf8"),
    ) as { navigation: { items: { adManagementOrganizationalUnits: string } } };
    const enNavigation = JSON.parse(
      readFileSync(new URL("../../locales/en/navigation.json", import.meta.url), "utf8"),
    ) as { navigation: { items: { adManagementOrganizationalUnits: string } } };

    assert.equal(
      trNavigation.navigation.items.adManagementOrganizationalUnits,
      "Organizasyon Birimleri",
    );
    assert.equal(
      enNavigation.navigation.items.adManagementOrganizationalUnits,
      "Organizational units",
    );
  });

  it("does not leave raw visible organizationalUnits strings in page sources", () => {
    const sources = [
      readFileSync(new URL("./AdOrganizationalUnitsPage.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./AdOrganizationalUnitDetailPage.tsx", import.meta.url), "utf8"),
      readFileSync(
        new URL("./components/AdOrganizationalUnitsSearchToolbar.tsx", import.meta.url),
        "utf8",
      ),
    ];

    for (const source of sources) {
      assert.doesNotMatch(source, /"Organizasyon Birimleri"/);
      assert.doesNotMatch(source, /"Organizational units"/);
      assert.match(source, /t\("(adManagement:)?organizationalUnits\./);
    }
  });
});
