import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import trAdManagement from "../../locales/tr/adManagement.json" with { type: "json" };
import enAdManagement from "../../locales/en/adManagement.json" with { type: "json" };
import trLogs from "../../locales/tr/adOperationLogs.json" with { type: "json" };
import enLogs from "../../locales/en/adOperationLogs.json" with { type: "json" };

describe("restoreAdDeletedObject API", () => {
  const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");

  it("posts to deleted object restore endpoint", () => {
    assert.match(apiSource, /restoreAdDeletedObject/);
    assert.match(apiSource, /\/ad-management\/deleted-objects\/\$\{id\}\/restore/);
    assert.match(apiSource, /AdDeletedObjectRestoreResponse/);
  });

  it("invalidates deleted objects and operation logs queries", () => {
    assert.match(apiSource, /invalidateAdManagementDeletedObjectRestoreQueries/);
    assert.match(apiSource, /AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY/);
    assert.match(apiSource, /AD_OPERATION_LOGS_QUERY_KEY/);
  });
});

describe("deleted object restore list actions", () => {
  const columnsSource = readFileSync(
    new URL("./ad-deleted-objects-columns.tsx", import.meta.url),
    "utf8",
  );
  const pageSource = readFileSync(new URL("./AdDeletedObjectsPage.tsx", import.meta.url), "utf8");

  it("includes restore action in row actions", () => {
    assert.match(columnsSource, /deletedObjects\.actions\.restore/);
    assert.match(columnsSource, /canRestoreDeletedObject/);
  });

  it("uses permission controlled restore on list page", () => {
    assert.match(pageSource, /AdManagement\.DeletedObjects\.Restore/);
    assert.match(pageSource, /AdDeletedObjectRestoreConfirmDialog/);
    assert.match(pageSource, /invalidateAdManagementDeletedObjectRestoreQueries/);
  });
});

describe("deleted object restore detail actions", () => {
  const detailActionsSource = readFileSync(
    new URL("./components/AdDeletedObjectDetailHeaderActions.tsx", import.meta.url),
    "utf8",
  );
  const detailPageSource = readFileSync(
    new URL("./AdDeletedObjectDetailPage.tsx", import.meta.url),
    "utf8",
  );

  it("uses permission controlled restore action on detail header", () => {
    assert.match(detailActionsSource, /AdManagement\.DeletedObjects\.Restore/);
    assert.match(detailActionsSource, /canRestoreDeletedObject/);
    assert.match(detailActionsSource, /AdDeletedObjectRestoreConfirmDialog/);
    assert.match(detailPageSource, /AdDeletedObjectDetailHeaderActions/);
  });

  it("navigates to returnPath after successful restore", () => {
    assert.match(detailActionsSource, /navigate\(returnPath\)/);
  });
});

describe("deleted object restore eligibility", () => {
  const eligibilitySource = readFileSync(
    new URL("./ad-deleted-object-restore-eligibility.ts", import.meta.url),
    "utf8",
  );

  it("hides restore for unsupported type and missing lastKnownParent", () => {
    assert.match(eligibilitySource, /isRestorableDeletedObjectType/);
    assert.match(eligibilitySource, /lastKnownParent/);
    assert.match(eligibilitySource, /lastKnownRdn/);
  });
});

describe("deleted object restore typed confirmation", () => {
  const dialogSource = readFileSync(
    new URL("./components/AdDeletedObjectRestoreConfirmDialog.tsx", import.meta.url),
    "utf8",
  );

  it("requires primary label typed confirmation", () => {
    assert.match(dialogSource, /getAdDeletedObjectPrimaryLabel/);
    assert.match(dialogSource, /confirmValue\.trim\(\)\.toLowerCase\(\)/);
    assert.match(dialogSource, /deletedObjects\.restore\.dialogTitle/);
    assert.match(dialogSource, /deletedObjects\.restore\.targetLocation/);
  });
});

describe("deleted object restore i18n", () => {
  it("has parallel TR restore keys", () => {
    assert.equal(trAdManagement.adManagement.deletedObjects.actions.restore, "Geri Yükle");
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.dialogTitle,
      "Silinen nesneyi geri yükle",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.success.restore,
      "Silinen nesne geri yüklendi.",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.errors.restoreFailed,
      "Silinen nesne geri yüklenemedi.",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.operationLogs.operationTypes.DeletedObjectRestore,
      "Silinen nesne geri yükleme",
    );
  });

  it("has parallel EN restore keys", () => {
    assert.equal(enAdManagement.adManagement.deletedObjects.actions.restore, "Restore");
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.dialogTitle,
      "Restore deleted object",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.success.restore,
      "Deleted object was restored.",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.errors.restoreFailed,
      "Deleted object could not be restored.",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.operationLogs.operationTypes.DeletedObjectRestore,
      "Deleted object restore",
    );
  });

  it("includes operation log labels in adOperationLogs namespace", () => {
    assert.equal(trLogs.adOperationLogs.operations.DeletedObjectRestore, "Silinen nesne geri yükleme");
    assert.equal(enLogs.adOperationLogs.operations.DeletedObjectRestore, "Deleted object restore");
  });
});

describe("deleted object restore snapshot renderer", () => {
  const parserSource = readFileSync(
    new URL("./parse-ad-operation-snapshot.ts", import.meta.url),
    "utf8",
  );
  const detailSource = readFileSync(
    new URL("./components/AdOperationLogSnapshotDetail.tsx", import.meta.url),
    "utf8",
  );

  it("uses deletedObjectRestore snapshot strategy", () => {
    assert.equal(
      readFileSync(new URL("./parse-ad-operation-snapshot.ts", import.meta.url), "utf8").includes(
        'operationType === "DeletedObjectRestore"',
      ),
      true,
    );
    assert.match(parserSource, /deletedObjectRestore/);
    assert.match(detailSource, /DeletedObjectRestoreSnapshotSections/);
    assert.match(detailSource, /snapshotSections\.deletedObject/);
    assert.match(detailSource, /snapshotSections\.restoreTarget/);
    assert.match(detailSource, /snapshotSections\.restoredObject/);
  });
});
