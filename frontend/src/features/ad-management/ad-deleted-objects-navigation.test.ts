import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import { buildAdDeletedObjectDetailPath } from "./ad-deleted-object-detail-path.ts";
import {
  AD_DELETED_OBJECTS_LIST_DEFAULTS,
  AD_DELETED_OBJECTS_LIST_PATH,
  normalizeAdDeletedObjectsListState,
  parseAdDeletedObjectsListStateFromSession,
} from "./ad-deleted-objects-list-query.ts";
import {
  getAdDeletedObjectPrimaryLabel,
  getAdDeletedObjectSecondaryLabel,
} from "./ad-deleted-object-display-labels.ts";

const objectId = "550e8400-e29b-41d4-a716-446655440000";

describe("ad deleted objects navigation", () => {
  it("builds deleted object detail path", () => {
    assert.equal(
      buildAdDeletedObjectDetailPath(objectId),
      `${AD_DELETED_OBJECTS_LIST_PATH}/${objectId}`,
    );
  });

  it("protects deleted objects routes with AdManagement.DeletedObjects.View permission", () => {
    const routerSource = readFileSync(
      new URL("../../app/router.tsx", import.meta.url),
      "utf8",
    );

    assert.match(routerSource, /path: "\/ad-management\/deleted-objects"/);
    assert.match(routerSource, /path: "\/ad-management\/deleted-objects\/:id"/);
    assert.match(routerSource, /RequirePermission permission="AdManagement\.DeletedObjects\.View"/);
    assert.match(routerSource, /AdDeletedObjectsPage/);
    assert.match(routerSource, /AdDeletedObjectDetailPage/);
  });

  it("shows deleted objects menu item only for deleted objects permission", () => {
    const sidebarSource = readFileSync(
      new URL("../../components/layout/sidebar-items.ts", import.meta.url),
      "utf8",
    );

    assert.match(sidebarSource, /AdManagement\.DeletedObjects\.View/);
    assert.match(sidebarSource, /to: "\/ad-management\/deleted-objects"/);
    assert.match(sidebarSource, /items\.adManagementDeletedObjects/);
    assert.match(sidebarSource, /isAdManagementDeletedObjectsVisible/);
    assert.match(
      sidebarSource,
      /isAdManagementDeletedObjectsVisible\(user, adManagementModule\)/,
    );
  });

  it("includes deleted objects visibility in AD Management parent section", () => {
    const sidebarSource = readFileSync(
      new URL("../../components/layout/sidebar-items.ts", import.meta.url),
      "utf8",
    );

    assert.match(sidebarSource, /isAdManagementDeletedObjectsVisible\(user, moduleState\)/);
  });

  it("uses list toolbar, filters, DataTable and pagination", () => {
    const pageSource = readFileSync(
      new URL("./AdDeletedObjectsPage.tsx", import.meta.url),
      "utf8",
    );
    const toolbarSource = readFileSync(
      new URL("./components/AdDeletedObjectsSearchToolbar.tsx", import.meta.url),
      "utf8",
    );
    const columnsSource = readFileSync(
      new URL("./ad-deleted-objects-columns.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /deletedObjects\.searchPlaceholder/);
    assert.match(toolbarSource, /deletedObjects\.filters\.type/);
    assert.match(pageSource, /DataTable/);
    assert.match(pageSource, /DataTablePagination/);
    assert.match(pageSource, /mode="directory"/);
    assert.match(columnsSource, /common:actions\.detail/);
    assert.doesNotMatch(pageSource, /restore/i);
    assert.doesNotMatch(columnsSource, /restore/i);
  });

  it("detail page includes summary, location and technical sections without restore action", () => {
    const detailSource = readFileSync(
      new URL("./AdDeletedObjectDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /deletedObjects\.sections\.summary/);
    assert.match(detailSource, /deletedObjects\.sections\.location/);
    assert.match(detailSource, /deletedObjects\.sections\.technical/);
    assert.match(detailSource, /deletedObjects\.sections\.additionalAttributes/);
    assert.match(detailSource, /common:actions\.back/);
    assert.match(detailSource, /common:actions\.refresh/);
    assert.doesNotMatch(detailSource, /restore/i);
  });

  it("uses correct API endpoints", () => {
    const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");

    assert.match(apiSource, /\/ad-management\/deleted-objects/);
    assert.match(apiSource, /export const getAdDeletedObjects/);
    assert.match(apiSource, /export const getAdDeletedObjectById/);
    assert.match(apiSource, /AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY/);
    assert.match(apiSource, /\/ad-management\/deleted-objects\/\$\{id\}/);
  });

  it("normalizes deleted objects list state with type filter", () => {
    const normalized = normalizeAdDeletedObjectsListState({ type: "user", pageNumber: 2 });
    assert.equal(normalized.type, "user");
    assert.equal(normalized.pageNumber, 2);
    assert.equal(AD_DELETED_OBJECTS_LIST_DEFAULTS.type, "all");
    assert.equal(AD_DELETED_OBJECTS_LIST_DEFAULTS.includeAll, false);
  });
});

describe("ad deleted objects list all", () => {
  it("defaults includeAll to false in list state", () => {
    assert.equal(AD_DELETED_OBJECTS_LIST_DEFAULTS.includeAll, false);
    assert.equal(normalizeAdDeletedObjectsListState({}).includeAll, false);
  });

  it("parses includeAll from session state", () => {
    const parsed = parseAdDeletedObjectsListStateFromSession(
      JSON.stringify({ includeAll: true, type: "all", pageNumber: 1 }),
    );
    assert.equal(parsed.includeAll, true);
  });

  it("clears includeAll when normalizing without includeAll true", () => {
    const normalized = normalizeAdDeletedObjectsListState({ includeAll: false });
    assert.equal(normalized.includeAll, false);
  });

  it("list all action sets includeAll true with empty search and all type", () => {
    const toolbarSource = readFileSync(
      new URL("./components/AdDeletedObjectsSearchToolbar.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /deletedObjects\.actions\.listAll/);
    assert.match(toolbarSource, /includeAll: true/);
    assert.match(toolbarSource, /AD_DELETED_OBJECTS_LIST_DEFAULTS\.search/);
    assert.match(toolbarSource, /AD_DELETED_OBJECTS_LIST_DEFAULTS\.type/);
    assert.match(toolbarSource, /pageNumber: AD_DELETED_OBJECTS_LIST_DEFAULTS\.pageNumber/);
  });

  it("resets includeAll on search and type filter changes", () => {
    const toolbarSource = readFileSync(
      new URL("./components/AdDeletedObjectsSearchToolbar.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /search: debouncedSearch[\s\S]*includeAll: false/);
    assert.match(toolbarSource, /type: event\.target\.value[\s\S]*includeAll: false/);
  });

  it("enables query when includeAll is true on list page", () => {
    const pageSource = readFileSync(
      new URL("./AdDeletedObjectsPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /listState\.includeAll/);
    assert.match(pageSource, /normalizedSearch\.length >= MIN_SEARCH_LENGTH \|\| hasTypeFilter \|\| listState\.includeAll/);
  });

  it("sends includeAll through API and query key", () => {
    const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");
    const pageSource = readFileSync(
      new URL("./AdDeletedObjectsPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(apiSource, /includeAll: params\.includeAll/);
    assert.match(pageSource, /includeAll: listState\.includeAll/);
    assert.match(pageSource, /listState\.includeAll/);
  });

  it("shows list all active badge and warning when includeAll is true", () => {
    const toolbarSource = readFileSync(
      new URL("./components/AdDeletedObjectsSearchToolbar.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /deletedObjects\.actions\.listAllActive/);
    assert.match(toolbarSource, /deletedObjects\.warnings\.listAll/);
    assert.match(toolbarSource, /listState\.includeAll \?/);
  });

  it("does not disable refresh when includeAll is true", () => {
    const toolbarSource = readFileSync(
      new URL("./components/AdDeletedObjectsSearchToolbar.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /onClick=\{onRefresh\} disabled=\{!canSearch\}/);
  });

  it("preserves includeAll in list return path query", () => {
    const listStateSource = readFileSync(
      new URL("./use-ad-deleted-object-list-state.ts", import.meta.url),
      "utf8",
    );
    const returnPathSource = readFileSync(
      new URL("./ad-deleted-objects-return-path.ts", import.meta.url),
      "utf8",
    );
    const pageSource = readFileSync(
      new URL("./AdDeletedObjectsPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(listStateSource, /params\.set\("includeAll", "true"\)/);
    assert.match(returnPathSource, /buildAdDeletedObjectsListReturnState\(/);
    assert.match(pageSource, /buildAdDeletedObjectsListReturnState\(listPath\)/);
  });
});

describe("ad deleted objects display labels", () => {
  it("uses displayName as primary label", () => {
    const item = {
      displayName: "Deleted User",
      name: "user1",
      samAccountName: "user1",
      userPrincipalName: "user1@corp.local",
    };

    assert.equal(getAdDeletedObjectPrimaryLabel(item), "Deleted User");
    assert.equal(
      getAdDeletedObjectSecondaryLabel(item, "Deleted User"),
      "user1",
    );
  });
});

describe("ad deleted objects i18n", () => {
  it("defines deletedObjects keys in TR and EN adManagement locales", () => {
    const trAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        deletedObjects: {
          title: string;
          list: { pageTitle: string };
          detail: { pageTitle: string };
          filters: { typeUser: string; typeGroup: string; typeComputer: string };
          empty: { searchRequired: string };
          warnings: { restoreNotAvailable: string; listAll: string };
          actions: { listAll: string; listAllActive: string };
        };
      };
    };
    const enAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        deletedObjects: {
          title: string;
          list: { pageTitle: string };
          detail: { pageTitle: string };
          filters: { typeUser: string; typeGroup: string; typeComputer: string };
          empty: { searchRequired: string };
          warnings: { restoreNotAvailable: string; listAll: string };
          actions: { listAll: string; listAllActive: string };
        };
      };
    };

    assert.equal(trAdManagement.adManagement.deletedObjects.title, "Silinen Nesneler");
    assert.equal(trAdManagement.adManagement.deletedObjects.list.pageTitle, "Silinen Nesneler");
    assert.equal(
      trAdManagement.adManagement.deletedObjects.detail.pageTitle,
      "Silinen Nesne Detayı",
    );
    assert.equal(trAdManagement.adManagement.deletedObjects.filters.typeUser, "Kullanıcı");
    assert.equal(trAdManagement.adManagement.deletedObjects.filters.typeGroup, "Grup");
    assert.equal(trAdManagement.adManagement.deletedObjects.filters.typeComputer, "Bilgisayar");
    assert.equal(
      trAdManagement.adManagement.deletedObjects.empty.searchRequired,
      "Silinen nesneleri aramak için en az 2 karakter girin veya filtre seçin.",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.warnings.restoreNotAvailable,
      "Geri yükleme bu fazda kullanılabilir değildir.",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.actions.listAll,
      "Tüm silinen nesneleri listele",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.actions.listAllActive,
      "Tüm silinen nesneler listeleniyor",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.warnings.listAll,
      "Bu görünüm, Deleted Objects kapsayıcısındaki desteklenen tüm silinen nesneleri sayfalı olarak listeler.",
    );

    assert.equal(enAdManagement.adManagement.deletedObjects.title, "Deleted objects");
    assert.equal(enAdManagement.adManagement.deletedObjects.list.pageTitle, "Deleted objects");
    assert.equal(
      enAdManagement.adManagement.deletedObjects.detail.pageTitle,
      "Deleted object detail",
    );
    assert.equal(enAdManagement.adManagement.deletedObjects.filters.typeUser, "User");
    assert.equal(enAdManagement.adManagement.deletedObjects.filters.typeGroup, "Group");
    assert.equal(enAdManagement.adManagement.deletedObjects.filters.typeComputer, "Computer");
    assert.equal(
      enAdManagement.adManagement.deletedObjects.empty.searchRequired,
      "Enter at least 2 characters or choose a filter to search deleted objects.",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.warnings.restoreNotAvailable,
      "Restore is not available in this phase.",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.actions.listAll,
      "List all deleted objects",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.actions.listAllActive,
      "Listing all deleted objects",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.warnings.listAll,
      "This view lists all supported deleted objects in the Deleted Objects container with pagination.",
    );
  });

  it("does not leave raw visible deletedObjects strings in page sources", () => {
    const sources = [
      readFileSync(new URL("./AdDeletedObjectsPage.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./AdDeletedObjectDetailPage.tsx", import.meta.url), "utf8"),
      readFileSync(
        new URL("./components/AdDeletedObjectsSearchToolbar.tsx", import.meta.url),
        "utf8",
      ),
    ];

    for (const source of sources) {
      assert.doesNotMatch(source, /"Silinen Nesneler"/);
      assert.doesNotMatch(source, /"Deleted objects"/);
      assert.match(source, /t\("(adManagement:)?deletedObjects\./);
    }
  });
});
