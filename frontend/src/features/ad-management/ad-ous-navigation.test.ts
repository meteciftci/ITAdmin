import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  AD_ORGANIZATIONAL_UNIT_CREATE_PATH,
  buildAdOrganizationalUnitCreatePath,
  buildAdOrganizationalUnitDetailPath,
  buildAdOrganizationalUnitMovePath,
  buildAdOrganizationalUnitRenamePath,
} from "./ad-ou-detail-path.ts";
import { AD_ORGANIZATIONAL_UNITS_LIST_PATH } from "./ad-ous-list-path.ts";
import {
  AD_OPERATION_LOG_COVERAGE_OPERATION_TYPES,
  buildAdOperationLogCoverageMatrix,
} from "./ad-operation-log-coverage-matrix.ts";
import { getSnapshotRenderStrategy } from "./parse-ad-operation-snapshot.ts";

const organizationalUnitId = "550e8400-e29b-41d4-a716-446655440000";
const parentDn = "OU=Departments,DC=corp,DC=local";

function readLocaleOperations(locale: "tr" | "en"): Record<string, string> {
  const source = readFileSync(
    new URL(`../../locales/${locale}/adOperationLogs.json`, import.meta.url),
    "utf8",
  );
  const parsed = JSON.parse(source) as { adOperationLogs: { operations: Record<string, string> } };
  return parsed.adOperationLogs.operations;
}

describe("ad organizational units navigation", () => {
  it("builds organizational unit detail, rename and move paths", () => {
    assert.equal(
      buildAdOrganizationalUnitDetailPath(organizationalUnitId),
      `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/${organizationalUnitId}`,
    );
    assert.equal(
      buildAdOrganizationalUnitRenamePath(organizationalUnitId),
      `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/${organizationalUnitId}/rename`,
    );
    assert.equal(
      buildAdOrganizationalUnitMovePath(organizationalUnitId),
      `${AD_ORGANIZATIONAL_UNITS_LIST_PATH}/${organizationalUnitId}/move`,
    );
  });

  it("builds organizational unit create path with optional parentDn query param", () => {
    assert.equal(buildAdOrganizationalUnitCreatePath(), AD_ORGANIZATIONAL_UNIT_CREATE_PATH);
    assert.equal(
      buildAdOrganizationalUnitCreatePath(parentDn),
      `${AD_ORGANIZATIONAL_UNIT_CREATE_PATH}?parentDn=${encodeURIComponent(parentDn)}`,
    );
  });

  it("protects organizational units routes with expected permissions", () => {
    const routerSource = readFileSync(
      new URL("../../app/router.tsx", import.meta.url),
      "utf8",
    );

    assert.match(routerSource, /path: "\/ad-management\/organizational-units"/);
    assert.match(routerSource, /path: "\/ad-management\/organizational-units\/create"/);
    assert.match(routerSource, /path: "\/ad-management\/organizational-units\/:id\/rename"/);
    assert.match(routerSource, /path: "\/ad-management\/organizational-units\/:id\/move"/);
    assert.match(routerSource, /path: "\/ad-management\/organizational-units\/:id"/);
    assert.match(
      routerSource,
      /RequirePermission permission=\{PermissionCodes\.AdManagement\.OrganizationalUnits\.View\}/,
    );
    assert.match(
      routerSource,
      /RequirePermission permission=\{PermissionCodes\.AdManagement\.OrganizationalUnits\.Create\}/,
    );
    assert.match(
      routerSource,
      /RequirePermission permission=\{PermissionCodes\.AdManagement\.OrganizationalUnits\.Update\}/,
    );
    assert.match(
      routerSource,
      /RequirePermission permission=\{PermissionCodes\.AdManagement\.OrganizationalUnits\.Move\}/,
    );
    assert.match(routerSource, /AdOrganizationalUnitsPage/);
    assert.match(routerSource, /AdOrganizationalUnitCreatePage/);
    assert.match(routerSource, /AdOrganizationalUnitRenamePage/);
    assert.match(routerSource, /AdOrganizationalUnitMovePage/);
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

  it("does not query list API until search has minimum length", () => {
    const pageSource = readFileSync(
      new URL("./AdOrganizationalUnitsPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /MIN_SEARCH_LENGTH = 2/);
    assert.match(pageSource, /canSearch = normalizedSearch\.length >= MIN_SEARCH_LENGTH/);
    assert.match(pageSource, /enabled: moduleStatus\.isOperational && canSearch/);
    assert.match(pageSource, /organizationalUnits\.empty\.searchRequired/);
    assert.doesNotMatch(pageSource, /AdRenameOrganizationalUnitDialog/);
    assert.doesNotMatch(pageSource, /renameTarget/);
  });

  it("uses list toolbar, DataTable, pagination, count badges and delete dialog", () => {
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
    const createPageSource = readFileSync(
      new URL("./AdOrganizationalUnitCreatePage.tsx", import.meta.url),
      "utf8",
    );
    const renamePageSource = readFileSync(
      new URL("./AdOrganizationalUnitRenamePage.tsx", import.meta.url),
      "utf8",
    );

    const movePageSource = readFileSync(
      new URL("./AdOrganizationalUnitMovePage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /organizationalUnits\.searchPlaceholder/);
    assert.match(toolbarSource, /canSearch/);
    assert.match(toolbarSource, /AD_ORGANIZATIONAL_UNIT_CREATE_PATH/);
    assert.match(toolbarSource, /disabled=\{!canSearch\}/);
    assert.match(pageSource, /DataTable/);
    assert.match(pageSource, /DataTablePagination/);
    assert.match(pageSource, /buildAdOrganizationalUnitCreatePath/);
    assert.match(pageSource, /buildAdOrganizationalUnitRenamePath/);
    assert.match(pageSource, /buildAdOrganizationalUnitMovePath/);
    assert.doesNotMatch(pageSource, /moveTarget/);
    assert.doesNotMatch(pageSource, /AdMoveOrganizationalUnitDialog/);
    assert.match(columnsSource, /AdOrganizationalUnitCountBadge/);
    assert.match(columnsSource, /organizationalUnits\.table\.organizationalUnit/);
    assert.match(columnsSource, /getAdOrganizationalUnitPrimaryLabel/);
    assert.doesNotMatch(dialogsSource, /AdMoveOrganizationalUnitDialog/);
    assert.match(dialogsSource, /AdDeleteOrganizationalUnitDialog/);
    assert.match(movePageSource, /moveAdOrganizationalUnit/);
    assert.match(movePageSource, /excludeDistinguishedName=\{organizationalUnit\.distinguishedName\}/);
    assert.match(movePageSource, /searchContext="manage"/);
    assert.match(movePageSource, /disabled=\{!canSubmit\}/);
    assert.match(movePageSource, /buildAdOrganizationalUnitDetailPath/);
    assert.match(createPageSource, /createAdOrganizationalUnit/);
    assert.match(renamePageSource, /renameAdOrganizationalUnit/);
    assert.match(renamePageSource, /organizationalUnits\.rename\.pageTitle/);
    assert.match(movePageSource, /organizationalUnits\.move\.pageTitle/);
  });

  it("detail page uses technical fields, count stat cards and rename route navigation", () => {
    const detailSource = readFileSync(
      new URL("./AdOrganizationalUnitDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /AdOrganizationalUnitTechnicalField/);
    assert.match(detailSource, /AdOrganizationalUnitCountBadge/);
    assert.match(detailSource, /organizationalUnits\.detail\.sections\.overview/);
    assert.match(detailSource, /organizationalUnits\.detail\.sections\.contentSummary/);
    assert.match(detailSource, /organizationalUnits\.detail\.sections\.childOrganizationalUnits/);
    assert.match(detailSource, /buildAdOrganizationalUnitRenamePath/);
    assert.match(detailSource, /buildAdOrganizationalUnitMovePath/);
    assert.doesNotMatch(detailSource, /moveOpen/);
    assert.doesNotMatch(detailSource, /AdMoveOrganizationalUnitDialog/);
    assert.match(detailSource, /line-clamp-2/);
    assert.doesNotMatch(detailSource, /AdRenameOrganizationalUnitDialog/);
    assert.doesNotMatch(detailSource, /renameOpen/);
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
          empty: { searchRequired: string };
          create: { pageTitle: string; messages: { created: string } };
          rename: { pageTitle: string; messages: { renamed: string } };
          move: { pageTitle: string; messages: { moved: string } };
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
          empty: { searchRequired: string };
          create: { pageTitle: string; messages: { created: string } };
          rename: { pageTitle: string; messages: { renamed: string } };
          move: { pageTitle: string; messages: { moved: string } };
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
      trAdManagement.adManagement.organizationalUnits.empty.searchRequired,
      "Organizasyon birimi aramak için en az 2 karakter girin.",
    );
    assert.equal(
      trAdManagement.adManagement.organizationalUnits.create.pageTitle,
      "OU Oluştur",
    );
    assert.equal(
      trAdManagement.adManagement.organizationalUnits.rename.pageTitle,
      "OU Yeniden Adlandır",
    );
    assert.equal(
      trAdManagement.adManagement.organizationalUnits.move.pageTitle,
      "OU Taşı",
    );
    assert.equal(
      trAdManagement.adManagement.organizationalUnits.detail.title,
      "OU Detayı",
    );
    assert.ok(trAdManagement.adManagement.apiMessages.organizationalUnits.notEmpty);

    assert.equal(enAdManagement.adManagement.organizationalUnits.title, "Organizational units");
    assert.equal(
      enAdManagement.adManagement.organizationalUnits.rename.pageTitle,
      "Rename OU",
    );
    assert.equal(
      enAdManagement.adManagement.organizationalUnits.move.pageTitle,
      "Move OU",
    );
    assert.ok(enAdManagement.adManagement.apiMessages.organizationalUnits.notEmpty);
  });

  it("defines navigation keys for organizational units", () => {
    const trNavigation = JSON.parse(
      readFileSync(new URL("../../locales/tr/navigation.json", import.meta.url), "utf8"),
    ) as {
      navigation: {
        items: {
          adManagementOrganizationalUnits: string;
          adManagementOrganizationalUnitsCreate: string;
          adManagementOrganizationalUnitsRename: string;
          adManagementOrganizationalUnitsMove: string;
        };
      };
    };
    const enNavigation = JSON.parse(
      readFileSync(new URL("../../locales/en/navigation.json", import.meta.url), "utf8"),
    ) as {
      navigation: {
        items: {
          adManagementOrganizationalUnits: string;
          adManagementOrganizationalUnitsCreate: string;
          adManagementOrganizationalUnitsRename: string;
          adManagementOrganizationalUnitsMove: string;
        };
      };
    };

    assert.equal(
      trNavigation.navigation.items.adManagementOrganizationalUnitsRename,
      "OU Yeniden Adlandır",
    );
    assert.equal(
      trNavigation.navigation.items.adManagementOrganizationalUnitsMove,
      "OU Taşı",
    );
    assert.equal(
      enNavigation.navigation.items.adManagementOrganizationalUnitsRename,
      "Rename OU",
    );
    assert.equal(
      enNavigation.navigation.items.adManagementOrganizationalUnitsMove,
      "Move OU",
    );
  });

  it("does not leave raw visible organizationalUnits strings in page sources", () => {
    const sources = [
      readFileSync(new URL("./AdOrganizationalUnitsPage.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./AdOrganizationalUnitDetailPage.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./AdOrganizationalUnitCreatePage.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./AdOrganizationalUnitRenamePage.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./AdOrganizationalUnitMovePage.tsx", import.meta.url), "utf8"),
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
