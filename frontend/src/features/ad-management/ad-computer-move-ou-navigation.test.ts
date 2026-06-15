import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  buildAdComputerDetailPath,
  buildAdComputerMoveOuPath,
} from "./ad-computer-detail-path.ts";

const computerId = "550e8400-e29b-41d4-a716-446655440000";

describe("AD computer move OU navigation", () => {
  it("builds computer move OU path", () => {
    assert.equal(
      buildAdComputerMoveOuPath(computerId),
      `/ad-management/computers/${computerId}/move-ou`,
    );
  });

  it("builds detail path for post-move redirect", () => {
    assert.equal(
      buildAdComputerDetailPath(computerId),
      `/ad-management/computers/${computerId}`,
    );
  });
});

describe("AdMoveComputerOuPage wiring", () => {
  it("loads computer detail and uses move OU form", () => {
    const pageSource = readFileSync(
      new URL("./AdMoveComputerOuPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /getAdComputerById/);
    assert.match(pageSource, /AdComputerMoveOuForm/);
    assert.match(pageSource, /targetOuDistinguishedName/);
    assert.match(pageSource, /sameOuWarning/);
    assert.match(pageSource, /disabled=\{!canSubmit\}/);
    assert.match(pageSource, /moveAdComputerOu/);
    assert.match(pageSource, /invalidateAdManagementComputerQueries/);
    assert.match(pageSource, /isAdComputerAccountOperationRestricted/);
  });

  it("returns to computer detail after success", () => {
    const pageSource = readFileSync(
      new URL("./AdMoveComputerOuPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /buildAdComputerDetailPath\(computerId\)/);
    assert.match(pageSource, /computers\.moveOu\.messages\.moved/);
  });
});

describe("computer list and detail move OU actions", () => {
  it("shows move OU in list actions when permission is granted", () => {
    const columnsSource = readFileSync(
      new URL("./ad-computers-columns.tsx", import.meta.url),
      "utf8",
    );
    const pageSource = readFileSync(
      new URL("./AdComputersPage.tsx", import.meta.url),
      "utf8",
    );

    assert.match(columnsSource, /canMoveOu/);
    assert.match(columnsSource, /computers\.actions\.moveOu/);
    assert.match(columnsSource, /onMoveOu/);
    assert.match(pageSource, /AdManagement\.Computers\.MoveOu/);
    assert.match(pageSource, /buildAdComputerMoveOuPath/);
    assert.match(columnsSource, /computers\.actions\.enable/);
    assert.match(columnsSource, /computers\.actions\.disable/);
  });

  it("navigates to move OU route from detail operations menu", () => {
    const detailActionsSource = readFileSync(
      new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url),
      "utf8",
    );
    const rowActionsBlock = detailActionsSource.slice(
      detailActionsSource.indexOf("<RowActions"),
      detailActionsSource.indexOf("</RowActions>") + "</RowActions>".length,
    );

    assert.match(rowActionsBlock, /buildAdComputerMoveOuPath/);
    assert.match(rowActionsBlock, /computers\.actions\.moveOu/);
    assert.doesNotMatch(
      detailActionsSource,
      /showMoveOu\s*\?\s*\(\s*<Button[\s\S]*buildAdComputerMoveOuPath/,
    );
    assert.doesNotMatch(detailActionsSource, /AdComputerMoveOuDialog/);
    assert.doesNotMatch(detailActionsSource, /moveAdComputerOu/);
  });

  it("protects move OU route with Computers.MoveOu permission", () => {
    const routerSource = readFileSync(
      new URL("../../app/router.tsx", import.meta.url),
      "utf8",
    );

    assert.match(routerSource, /path: "\/ad-management\/computers\/:id\/move-ou"/);
    assert.match(routerSource, /RequirePermission permission="AdManagement\.Computers\.MoveOu"/);
    assert.match(routerSource, /AdMoveComputerOuPage/);
  });
});

describe("computer move OU API and form", () => {
  it("posts to move-ou endpoint and searches computer OUs", () => {
    const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");
    const formSource = readFileSync(
      new URL("./components/AdComputerMoveOuForm.tsx", import.meta.url),
      "utf8",
    );

    assert.match(apiSource, /moveAdComputerOu/);
    assert.match(apiSource, /\/ad-management\/computers\/\$\{computerId\}\/move-ou/);
    assert.match(apiSource, /searchComputerOrganizationalUnits/);
    assert.match(apiSource, /AD_OPERATION_LOGS_QUERY_KEY/);
    assert.match(formSource, /searchContext="computers"/);
  });
});

describe("computer move OU i18n", () => {
  it("defines page keys in TR and EN adManagement locales", () => {
    const trAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        computers: {
          moveOu: {
            pageTitle: string;
            pageDescription: string;
            sections: { computerSummary: string; targetOu: string };
            fields: { name: string; samAccountName: string };
            actions: { submit: string };
            protected: string;
            sameOu: string;
            messages: { moved: string; moveFailed: string };
          };
        };
      };
    };
    const enAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        computers: {
          moveOu: {
            pageTitle: string;
            pageDescription: string;
            sections: { computerSummary: string; targetOu: string };
            fields: { name: string; samAccountName: string };
            actions: { submit: string };
            protected: string;
            sameOu: string;
            messages: { moved: string; moveFailed: string };
          };
        };
      };
    };

    assert.match(trAdManagement.adManagement.computers.moveOu.pageTitle, /OU Taşı/i);
    assert.match(trAdManagement.adManagement.computers.moveOu.sections.computerSummary, /özeti/i);
    assert.equal(trAdManagement.adManagement.computers.moveOu.actions.submit, "Taşı");
    assert.match(trAdManagement.adManagement.computers.moveOu.protected, /taşınamaz/i);
    assert.equal(enAdManagement.adManagement.computers.moveOu.actions.submit, "Move");
    assert.match(enAdManagement.adManagement.computers.moveOu.pageTitle, /Move Computer OU/i);
  });

  it("does not leave raw i18n keys in move OU UI sources", () => {
    const sources = [
      readFileSync(new URL("./AdMoveComputerOuPage.tsx", import.meta.url), "utf8"),
      readFileSync(new URL("./components/AdComputerMoveOuForm.tsx", import.meta.url), "utf8"),
      readFileSync(
        new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url),
        "utf8",
      ),
    ];

    for (const source of sources) {
      assert.doesNotMatch(source, /"computers\.moveOu\.pageTitle"/);
      assert.match(source, /t\("(adManagement:)?computers\./);
    }
  });
});
