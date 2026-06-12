import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  AD_COMPUTERS_LIST_DEFAULTS,
  normalizeAdComputersListState,
  parseAdComputersListStateFromSession,
} from "./ad-computers-list-query.ts";
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

  it("uses list toolbar and account operation actions with permissions", () => {
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
    const detailActionsSource = readFileSync(
      new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /searchPlaceholder=\{t\("adManagement:computers\.searchPlaceholder"\)\}/);
    assert.match(toolbarSource, /computers\.filters\.active/);
    assert.doesNotMatch(toolbarSource, /canCreate|actions\.create|actions\.edit|actions\.delete|moveOu/i);
    assert.match(pageSource, /AdManagement\.Computers\.Enable/);
    assert.match(pageSource, /AdManagement\.Computers\.Disable/);
    assert.match(pageSource, /invalidateAdManagementComputerQueries/);
    assert.match(pageSource, /computers\.confirm\.enableTitle/);
    assert.match(pageSource, /common:actions\.confirm/);
    assert.match(columnsSource, /getAdComputerPrimaryLabel/);
    assert.match(columnsSource, /common:actions\.detail/);
    assert.match(columnsSource, /computers\.actions\.enable/);
    assert.match(columnsSource, /computers\.actions\.disable/);
    assert.match(columnsSource, /canEnableComputer && !computer\.isEnabled/);
    assert.match(columnsSource, /canDisableComputer && computer\.isEnabled/);
    assert.match(detailSource, /computers\.detail\.summaryTitle/);
    assert.match(detailSource, /computers\.detail\.technicalTitle/);
    assert.match(detailSource, /computers\.detail\.operatingSystemTitle/);
    assert.match(detailSource, /memberOfTruncated/);
    assert.match(detailActionsSource, /computers\.confirm\.disableDescription/);
    assert.match(detailActionsSource, /invalidateAdManagementComputerQueries/);
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
    assert.match(apiSource, /\/ad-management\/computer-operating-systems/);
    assert.match(apiSource, /export const getAdComputerOperatingSystems/);
    assert.match(apiSource, /operatingSystem: params\.operatingSystem/);
    assert.match(apiSource, /AD_COMPUTER_OPERATING_SYSTEMS_QUERY_KEY/);
    assert.match(apiSource, /export const enableAdComputer/);
    assert.match(apiSource, /export const disableAdComputer/);
    assert.match(apiSource, /\/ad-management\/computers\/\$\{computerId\}\/enable/);
    assert.match(apiSource, /\/ad-management\/computers\/\$\{computerId\}\/disable/);
  });
});

describe("ad computer account operations i18n and operation logs", () => {
  it("defines computer operation keys in TR and EN adManagement locales", () => {
    const trAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        computers: {
          actions: { enable: string; disable: string };
          confirm: { enableTitle: string; disableTitle: string };
          messages: { enabled: string; disabled: string };
        };
      };
    };
    const enAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        computers: {
          actions: { enable: string; disable: string };
          confirm: { enableTitle: string; disableTitle: string };
          messages: { enabled: string; disabled: string };
        };
      };
    };

    assert.equal(trAdManagement.adManagement.computers.actions.enable, "Etkinleştir");
    assert.equal(trAdManagement.adManagement.computers.actions.disable, "Devre dışı bırak");
    assert.equal(enAdManagement.adManagement.computers.actions.enable, "Enable");
    assert.equal(enAdManagement.adManagement.computers.actions.disable, "Disable");
    assert.match(trAdManagement.adManagement.computers.confirm.enableTitle, /etkinleştir/i);
    assert.match(enAdManagement.adManagement.computers.confirm.enableTitle, /enable/i);
  });

  it("supports ComputerEnable and ComputerDisable in operation log labels", () => {
    const trLogs = JSON.parse(
      readFileSync(new URL("../../locales/tr/adOperationLogs.json", import.meta.url), "utf8"),
    ) as { adOperationLogs: { operations: Record<string, string> } };
    const enLogs = JSON.parse(
      readFileSync(new URL("../../locales/en/adOperationLogs.json", import.meta.url), "utf8"),
    ) as { adOperationLogs: { operations: Record<string, string> } };

    assert.equal(trLogs.adOperationLogs.operations.ComputerEnable, "Bilgisayar etkinleştirme");
    assert.equal(trLogs.adOperationLogs.operations.ComputerDisable, "Bilgisayar devre dışı bırakma");
    assert.equal(enLogs.adOperationLogs.operations.ComputerEnable, "Computer enable");
    assert.equal(enLogs.adOperationLogs.operations.ComputerDisable, "Computer disable");
  });

  it("does not leave raw i18n keys in computer operation UI sources", () => {
    const sources = [
      readFileSync(new URL("./AdComputersPage.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./ad-computers-columns.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url), "utf8"),
    ];

    for (const source of sources) {
      assert.doesNotMatch(source, /"computers\.actions\.enable"/);
      assert.doesNotMatch(source, /"computers\.confirm\.enableTitle"/);
      assert.match(source, /t\("adManagement:computers\./);
    }
  });
});

describe("ad computers operating system filter", () => {
  it("includes operating system select in toolbar", () => {
    const toolbarSource = readFileSync(
      new URL("./components/AdComputersSearchToolbar.tsx", import.meta.url),
      "utf8",
    );

    assert.match(toolbarSource, /getAdComputerOperatingSystems/);
    assert.match(toolbarSource, /AD_COMPUTER_OPERATING_SYSTEMS_QUERY_KEY/);
    assert.match(toolbarSource, /computers\.filters\.operatingSystem/);
    assert.match(toolbarSource, /computers\.filters\.operatingSystemAll/);
    assert.match(toolbarSource, /listState\.operatingSystem/);
  });

  it("normalizes and restores operatingSystem in list state", () => {
    const normalized = normalizeAdComputersListState({
      operatingSystem: "Windows 10",
    });
    assert.equal(normalized.operatingSystem, "Windows 10");

    const restored = parseAdComputersListStateFromSession(
      JSON.stringify({ operatingSystem: "Windows Server 2022" }),
    );
    assert.equal(restored.operatingSystem, "Windows Server 2022");

    const defaults = normalizeAdComputersListState({});
    assert.equal(defaults.operatingSystem, AD_COMPUTERS_LIST_DEFAULTS.operatingSystem);
  });

  it("counts operatingSystem in active filters and clears it", () => {
    const pageSource = readFileSync(
      new URL("./AdComputersPage.tsx", import.meta.url),
      "utf8",
    );
    const listQuerySource = readFileSync(
      new URL("./ad-computers-list-query.ts", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /listState\.operatingSystem/);
    assert.match(pageSource, /listState\.operatingSystem\.trim\(\)/);
    assert.match(pageSource, /operatingSystem: listState\.operatingSystem/);
    assert.equal(listQuerySource.includes("operatingSystem: \"\""), true);
  });
});

describe("common actions.clear i18n", () => {
  it("defines clear action in TR and EN common locales", () => {
    const trCommon = JSON.parse(
      readFileSync(new URL("../../locales/tr/common.json", import.meta.url), "utf8"),
    ) as { common: { actions: { clear: string } } };
    const enCommon = JSON.parse(
      readFileSync(new URL("../../locales/en/common.json", import.meta.url), "utf8"),
    ) as { common: { actions: { clear: string } } };

    assert.equal(trCommon.common.actions.clear, "Temizle");
    assert.equal(enCommon.common.actions.clear, "Clear");
  });

  it("uses common clear label in account expiration date picker", () => {
    const source = readFileSync(
      new URL(
        "./components/ad-user-detail/AdUserAccountExpirationSection.tsx",
        import.meta.url,
      ),
      "utf8",
    );

    assert.match(source, /clearLabel=\{t\("common:actions\.clear"\)\}/);
    assert.doesNotMatch(source, /clearLabel=\{t\("actions\.clear"\)\}/);
  });
});
