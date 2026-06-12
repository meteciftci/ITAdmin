import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("ad computer delete", () => {
  it("deleteAdComputer sends DELETE to the correct endpoint", () => {
    const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");

    assert.match(apiSource, /export const deleteAdComputer/);
    assert.match(apiSource, /apiClient\.delete<DeleteAdComputerResponse>/);
    assert.match(apiSource, /\/ad-management\/computers\/\$\{computerId\}/);
  });

  it("requires typed confirmation in AdComputerDeleteConfirmDialog", () => {
    const source = readFileSync(
      new URL("./components/AdComputerDeleteConfirmDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /computers\.delete\.description/);
    assert.match(source, /computers\.delete\.confirmLabel/);
    assert.match(source, /computers\.delete\.confirmHint/);
    assert.match(source, /resolveConfirmationValue/);
    assert.match(source, /toLowerCase\(\)/);
    assert.match(source, /disabled=\{!isConfirmMatch \|\| isDeleting\}/);
    assert.match(source, /variant="destructive"/);
    assert.match(source, /setConfirmValue\(""\)/);
  });

  it("binds delete actions to AdManagement.Computers.Delete permission", () => {
    const pageSource = readFileSync(new URL("./AdComputersPage.tsx", import.meta.url), "utf8");
    const detailSource = readFileSync(new URL("./AdComputerDetailPage.tsx", import.meta.url), "utf8");
    const columnsSource = readFileSync(
      new URL("./ad-computers-columns.tsx", import.meta.url),
      "utf8",
    );
    const detailActionsSource = readFileSync(
      new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url),
      "utf8",
    );

    assert.match(pageSource, /AdManagement\.Computers\.Delete/);
    assert.match(pageSource, /canDeleteComputer/);
    assert.match(detailSource, /AdManagement\.Computers\.Delete/);
    assert.match(detailSource, /canDeleteComputer/);
    assert.match(columnsSource, /canDeleteComputer/);
    assert.match(columnsSource, /onDelete/);
    assert.match(detailActionsSource, /canDeleteComputer/);
    assert.match(detailActionsSource, /showDelete/);
    assert.match(detailActionsSource, /deleteAdComputer/);
  });

  it("hides protected computer delete action on detail header", () => {
    const source = readFileSync(
      new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /isAdComputerAccountOperationRestricted/);
    assert.match(source, /showDelete = canDeleteComputer && !isProtected/);
  });

  it("navigates to return path and invalidates queries after successful detail delete", () => {
    const source = readFileSync(
      new URL("./components/AdComputerDetailHeaderActions.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /invalidateAdManagementComputerQueries/);
    assert.match(source, /navigate\(returnPath\)/);
    assert.match(source, /computers\.delete\.success/);
  });

  it("invalidates computer queries after successful list delete", () => {
    const source = readFileSync(new URL("./AdComputersPage.tsx", import.meta.url), "utf8");

    assert.match(source, /AdComputerDeleteConfirmDialog/);
    assert.match(source, /invalidateAdManagementComputerQueries/);
    assert.match(source, /computers\.delete\.success/);
  });

  it("defines computer delete keys in TR and EN locales", () => {
    const trAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        computers: {
          delete: {
            title: string;
            success: string;
            error: string;
            confirmHint: string;
          };
        };
      };
    };
    const enAdManagement = JSON.parse(
      readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
    ) as {
      adManagement: {
        computers: {
          delete: {
            title: string;
            success: string;
            error: string;
            confirmHint: string;
          };
        };
      };
    };

    assert.match(trAdManagement.adManagement.computers.delete.title, /silinsin mi/i);
    assert.match(trAdManagement.adManagement.computers.delete.confirmHint, /yazın/i);
    assert.equal(trAdManagement.adManagement.computers.delete.success, "Bilgisayar hesabı silindi.");
    assert.match(enAdManagement.adManagement.computers.delete.title, /delete computer account/i);
    assert.match(enAdManagement.adManagement.computers.delete.confirmHint, /confirm deletion/i);
    assert.equal(enAdManagement.adManagement.computers.delete.success, "Computer account deleted.");
  });

  it("supports ComputerDelete in operation log labels", () => {
    const trLogs = JSON.parse(
      readFileSync(new URL("../../locales/tr/adOperationLogs.json", import.meta.url), "utf8"),
    ) as { adOperationLogs: { operations: Record<string, string> } };
    const enLogs = JSON.parse(
      readFileSync(new URL("../../locales/en/adOperationLogs.json", import.meta.url), "utf8"),
    ) as { adOperationLogs: { operations: Record<string, string> } };

    assert.equal(trLogs.adOperationLogs.operations.ComputerDelete, "Bilgisayar silme");
    assert.equal(enLogs.adOperationLogs.operations.ComputerDelete, "Computer delete");
  });
});
