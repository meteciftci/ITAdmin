import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  AD_OPERATION_LOG_COVERAGE_OPERATION_TYPES,
  buildAdOperationLogCoverageMatrix,
  summarizeAdOperationLogCoverage,
} from "./ad-operation-log-coverage-matrix.ts";
import { getSnapshotRenderStrategy } from "./parse-ad-operation-snapshot.ts";

type LocaleOperations = Record<string, string>;

function readLocaleOperations(locale: "tr" | "en"): LocaleOperations {
  const source = readFileSync(
    new URL(`../../locales/${locale}/adOperationLogs.json`, import.meta.url),
    "utf8",
  );
  const parsed = JSON.parse(source) as { adOperationLogs: { operations: LocaleOperations } };
  return parsed.adOperationLogs.operations;
}

describe("AD operation log coverage matrix", () => {
  const trOperations = readLocaleOperations("tr");
  const enOperations = readLocaleOperations("en");
  const matrix = buildAdOperationLogCoverageMatrix(trOperations, enOperations);
  const summary = summarizeAdOperationLogCoverage(matrix);

  it("tracks all known backend operation types", () => {
    assert.equal(matrix.length, AD_OPERATION_LOG_COVERAGE_OPERATION_TYPES.length);
    assert.equal(summary.operationTypeCount, 29);
  });

  it("keeps TR/EN operation label keys parallel", () => {
    const trKeys = Object.keys(trOperations).sort();
    const enKeys = Object.keys(enOperations).sort();
    assert.deepEqual(trKeys, enKeys);
  });

  it("includes labels for recently added AD operation types", () => {
    const recentTypes = [
      "DeletedObjectRestore",
      "ComputerDelete",
      "ComputerMoveOu",
      "ComputerGroupAdd",
      "ComputerGroupRemove",
      "GroupMoveOu",
      "UserOuMove",
    ] as const;

    for (const operationType of recentTypes) {
      const row = matrix.find((entry) => entry.operationType === operationType);
      assert.ok(row, `Missing coverage row for ${operationType}`);
      assert.equal(row.frontendLabelExists, true, `${operationType} label coverage`);
      assert.equal(row.trLocaleExists, true, `${operationType} TR locale`);
      assert.equal(row.enLocaleExists, true, `${operationType} EN locale`);
    }
  });

  it("documents snapshot renderer coverage without failing on generic fallback", () => {
    const genericRows = matrix.filter((row) => !row.snapshotRendererExists);
    assert.deepEqual(
      genericRows.map((row) => row.operationType).sort(),
      [
        "AttributeMappingCreated",
        "AttributeMappingDeleted",
        "AttributeMappingUpdated",
        "ComputerUpdate",
        "SettingsUpdated",
        "SettingsValidated",
      ].sort(),
    );

    for (const row of genericRows) {
      assert.match(row.notes, /generic/i);
    }
  });

  it("maps dedicated snapshot strategies for priority operations", () => {
    const expectations: Record<string, string> = {
      DeletedObjectRestore: "deletedObjectRestore",
      ComputerDelete: "computerDelete",
      ComputerMoveOu: "ouMove",
      ComputerGroupAdd: "groupMembership",
      ComputerGroupRemove: "groupMembership",
      GroupMoveOu: "ouMove",
      UserOuMove: "ouMove",
      UserManagerUpdate: "userManagerUpdate",
      UserAccountExpirationUpdate: "userAccountExpirationUpdate",
      UserEnable: "accountStatus",
      UserDisable: "accountStatus",
      UserUnlock: "lockStatus",
      CreateUser: "userCreate",
      GroupCreate: "groupCreate",
      GroupUpdate: "groupUpdate",
      GroupDelete: "groupDelete",
      GroupMemberAdd: "groupMember",
      GroupMemberRemove: "groupMember",
    };

    for (const [operationType, strategy] of Object.entries(expectations)) {
      assert.equal(getSnapshotRenderStrategy(operationType), strategy);
      const row = matrix.find((entry) => entry.operationType === operationType);
      assert.ok(row?.snapshotRendererExists, `${operationType} snapshot renderer`);
    }
  });

  it("keeps generic renderer fallback available", () => {
    assert.equal(getSnapshotRenderStrategy("SettingsUpdated"), "generic");
    assert.equal(getSnapshotRenderStrategy("ComputerUpdate"), "generic");
  });

  it("exposes inventory summary for phase 20B.1", () => {
    assert.equal(summary.labelCoverage, summary.operationTypeCount);
    assert.equal(summary.localeGapOperations.length, 0);
    assert.equal(summary.snapshotRendererCoverage, 23);
    assert.ok(summary.genericFallbackOperations.length > 0);
  });
});

describe("AdOperationLogSnapshotDetail dedicated render wiring", () => {
  it("includes restore and computer delete dedicated sections", () => {
    const detailSource = readFileSync(
      new URL("./components/AdOperationLogSnapshotDetail.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /DeletedObjectRestoreSnapshotSections/);
    assert.match(detailSource, /case "deletedObjectRestore"/);
    assert.match(detailSource, /ComputerDeleteSnapshotSections/);
    assert.match(detailSource, /case "computerDelete"/);
    assert.match(detailSource, /default:\s*return\s*\(\s*<GenericSnapshotSections/);
  });
});
