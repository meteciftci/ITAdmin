import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import { buildAdComputerDetailPath } from "./ad-computer-detail-path.ts";
import {
  getAdComputerPrimaryLabel,
  getAdComputerSecondaryLabel,
} from "./ad-computer-display-labels.ts";
import { AD_COMPUTERS_LIST_PATH } from "./ad-computers-list-path.ts";
import {
  buildAdComputerDetailReturnState,
  buildAdComputersListReturnState,
  resolveAdComputerReturnPath,
  resolveSafeAdComputerReturnPath,
} from "./ad-computers-return-path.ts";

const computerId = "550e8400-e29b-41d4-a716-446655440000";
const listPath = AD_COMPUTERS_LIST_PATH;

describe("ad computers navigation", () => {
  it("builds computer detail path", () => {
    assert.equal(buildAdComputerDetailPath(computerId), `${listPath}/${computerId}`);
  });

  it("returns detail path from detail return state", () => {
    assert.equal(
      resolveAdComputerReturnPath(buildAdComputerDetailReturnState(computerId)),
      `${listPath}/${computerId}`,
    );
  });

  it("returns list path from list return state", () => {
    assert.equal(
      resolveAdComputerReturnPath(buildAdComputersListReturnState()),
      listPath,
    );
  });

  it("falls back to computers list when return state is missing", () => {
    assert.equal(resolveAdComputerReturnPath(undefined), listPath);
  });

  it("rejects unsafe return paths", () => {
    assert.equal(resolveSafeAdComputerReturnPath("/ad-management/computers/../../../etc"), listPath);
  });
});

describe("ad computer display labels", () => {
  it("uses name as primary label", () => {
    const computer = {
      name: "PC01",
      samAccountName: "PC01$",
      distinguishedName: "CN=PC01,OU=Computers,DC=corp,DC=local",
    };

    assert.equal(getAdComputerPrimaryLabel(computer), "PC01");
  });

  it("preserves trailing dollar in secondary samAccountName label", () => {
    const computer = {
      name: "PC01",
      samAccountName: "PC01$",
      distinguishedName: "CN=PC01,OU=Computers,DC=corp,DC=local",
    };
    const primary = getAdComputerPrimaryLabel(computer);
    const secondary = getAdComputerSecondaryLabel(computer, primary);

    assert.equal(secondary, "PC01$");
  });
});

describe("ad computers route and menu wiring", () => {
  it("protects computers routes with AdManagement.Computers.View permission", () => {
    const routerSource = readFileSync(
      new URL("../../app/router.tsx", import.meta.url),
      "utf8",
    );

    assert.match(routerSource, /path: "\/ad-management\/computers"/);
    assert.match(routerSource, /path: "\/ad-management\/computers\/:id"/);
    assert.match(routerSource, /RequirePermission permission="AdManagement\.Computers\.View"/);
  });

  it("shows computers menu item only for computers permission", () => {
    const sidebarSource = readFileSync(
      new URL("../../components/layout/sidebar-items.ts", import.meta.url),
      "utf8",
    );

    assert.match(sidebarSource, /AdManagement\.Computers\.View/);
    assert.match(sidebarSource, /to: "\/ad-management\/computers"/);
    assert.match(sidebarSource, /items\.adManagementComputers/);
    assert.match(sidebarSource, /isAdManagementComputersVisible/);
    assert.match(sidebarSource, /isAdManagementComputersVisible\(user, adManagementModule\)/);
  });

  it("includes computers visibility in AD Management parent section", () => {
    const sidebarSource = readFileSync(
      new URL("../../components/layout/sidebar-items.ts", import.meta.url),
      "utf8",
    );

    assert.match(sidebarSource, /isAdManagementComputersVisible\(user, moduleState\)/);
  });

  it("uses read-only list toolbar and detail-only actions", () => {
    const pageSource = readFileSync(
      new URL("./AdComputersPage.tsx", import.meta.url),
      "utf8",
    );
    const toolbarSource = readFileSync(
      new URL("./components/AdComputersSearchToolbar.tsx", import.meta.url),
      "utf8",
    );
    const columnsSource = readFileSync(
      new URL("./ad-computers-columns.tsx", import.meta.url),
      "utf8",
    );
    const detailSource = readFileSync(
      new URL("./AdComputerDetailPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /searchPlaceholder=\{t\("adManagement:computers\.searchPlaceholder"\)\}/);
    assert.match(toolbarSource, /computers\.filters\.active/);
    assert.doesNotMatch(toolbarSource, /canCreate|actions\.create|actions\.edit|actions\.delete|moveOu/i);
    assert.doesNotMatch(pageSource, /canCreate|canUpdate|canDelete|canEnable|canDisable|canMoveOu/);
    assert.match(columnsSource, /getAdComputerPrimaryLabel/);
    assert.match(columnsSource, /computers\.actions\.detail/);
    assert.doesNotMatch(columnsSource, /computers\.actions\.(edit|delete|enable|disable)/);
    assert.match(detailSource, /computers\.detail\.summaryTitle/);
    assert.match(detailSource, /computers\.detail\.technicalTitle/);
    assert.match(detailSource, /computers\.detail\.operatingSystemTitle/);
    assert.match(detailSource, /memberOfTruncated/);
    assert.doesNotMatch(detailSource, /actions\.edit|actions\.delete|moveOu|manageMembers|canUpdate|canDelete/i);
  });

  it("uses correct API endpoints and parameters", () => {
    const apiSource = readFileSync(
      new URL("./api.ts", import.meta.url),
      "utf8",
    );

    assert.match(apiSource, /\/ad-management\/computers/);
    assert.match(apiSource, /pageNumber: params\.pageNumber/);
    assert.match(apiSource, /status: params\.status \?\? "active"/);
    assert.match(apiSource, /\/ad-management\/computers\/\$\{id\}/);
    assert.match(apiSource, /\/ad-management\/computer-organizational-units/);
    assert.match(apiSource, /export const getAdComputers/);
    assert.match(apiSource, /export const getAdComputerById/);
    assert.match(apiSource, /export const searchComputerOrganizationalUnits/);
  });
});
