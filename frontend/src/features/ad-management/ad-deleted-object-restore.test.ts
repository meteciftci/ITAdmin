import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import trAdManagement from "../../locales/tr/adManagement.json" with { type: "json" };
import enAdManagement from "../../locales/en/adManagement.json" with { type: "json" };
import trLogs from "../../locales/tr/adOperationLogs.json" with { type: "json" };
import enLogs from "../../locales/en/adOperationLogs.json" with { type: "json" };

describe("restoreAdDeletedObject API", () => {
  const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");
  const typesSource = readFileSync(
    new URL("./ad-deleted-object-restore-types.ts", import.meta.url),
    "utf8",
  );

  it("posts to deleted object restore endpoint with optional payload", () => {
    assert.match(apiSource, /restoreAdDeletedObject/);
    assert.match(apiSource, /\/ad-management\/deleted-objects\/\$\{id\}\/restore/);
    assert.match(apiSource, /AdDeletedObjectRestoreResponse/);
    assert.match(apiSource, /payload\?: RestoreAdDeletedObjectRequest/);
    assert.match(typesSource, /AdDeletedObjectRestoreTargetMode/);
    assert.match(typesSource, /OriginalLocation/);
    assert.match(typesSource, /TargetPath/);
  });

  it("invalidates deleted objects and operation logs queries", () => {
    assert.match(apiSource, /invalidateAdManagementDeletedObjectRestoreQueries/);
    assert.match(apiSource, /AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY/);
    assert.match(apiSource, /AD_OPERATION_LOGS_QUERY_KEY/);
  });
});

describe("deleted object restore routing", () => {
  const routerSource = readFileSync(new URL("../../app/router.tsx", import.meta.url), "utf8");
  const pathSource = readFileSync(
    new URL("./ad-deleted-object-detail-path.ts", import.meta.url),
    "utf8",
  );

  it("includes restore route with lazy import and permission guard", () => {
    assert.match(routerSource, /path: "\/ad-management\/deleted-objects\/:id\/restore"/);
    assert.match(routerSource, /AdDeletedObjectRestorePage/);
    assert.match(routerSource, /RequirePermission permission="AdManagement\.DeletedObjects\.Restore"/);
    assert.match(routerSource, /lazy\(\(\) =>\s*\n\s*import\("@\/features\/ad-management\/AdDeletedObjectRestorePage"\)/);
  });

  it("builds restore path helper", () => {
    assert.match(pathSource, /buildAdDeletedObjectRestorePath/);
    assert.match(pathSource, /\/restore/);
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

  it("navigates to restore page with returnPath instead of opening dialog", () => {
    assert.match(pageSource, /AdManagement\.DeletedObjects\.Restore/);
    assert.match(pageSource, /buildAdDeletedObjectRestorePath/);
    assert.match(pageSource, /state: \{ returnPath: listPath \}/);
    assert.doesNotMatch(pageSource, /AdDeletedObjectRestoreConfirmDialog/);
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
    assert.match(detailActionsSource, /buildAdDeletedObjectRestorePath/);
    assert.doesNotMatch(detailActionsSource, /AdDeletedObjectRestoreConfirmDialog/);
    assert.match(detailPageSource, /AdDeletedObjectDetailHeaderActions/);
  });

  it("navigates to restore page with returnPath", () => {
    assert.match(detailActionsSource, /state: \{ returnPath \}/);
  });
});

describe("deleted object restore confirmation value", () => {
  const utilsSource = readFileSync(
    new URL("./ad-deleted-object-restore-utils.ts", import.meta.url),
    "utf8",
  );

  it("defines sAMAccountName-first confirmation resolver", () => {
    assert.match(utilsSource, /getAdDeletedObjectRestoreConfirmationValue/);
    assert.match(utilsSource, /samAccountName/);
    assert.match(utilsSource, /displayName/);
    assert.match(utilsSource, /containsDeletedObjectRestoreNameMarker/);
  });
});

describe("deleted object restore page workflow", () => {
  const restorePageSource = readFileSync(
    new URL("./AdDeletedObjectRestorePage.tsx", import.meta.url),
    "utf8",
  );

  it("loads detail and uses typed confirmation from sAMAccountName-first value", () => {
    assert.match(restorePageSource, /getAdDeletedObjectById/);
    assert.match(restorePageSource, /getAdDeletedObjectRestoreConfirmationValue/);
    assert.match(restorePageSource, /usesSamAccountName/);
    assert.match(restorePageSource, /confirmValue\.trim\(\)\.toLowerCase\(\)/);
    assert.match(restorePageSource, /canRestoreDeletedObject/);
  });

  it("submits restore payloads aligned with backend contract", () => {
    assert.match(restorePageSource, /restoreAdDeletedObject/);
    assert.match(restorePageSource, /restoreTargetMode: "TargetPath"/);
    assert.match(restorePageSource, /targetPathDistinguishedName/);
    assert.match(restorePageSource, /return restoreAdDeletedObject\(id!\);/);
    assert.match(restorePageSource, /AdOuSearchCombobox/);
    assert.match(restorePageSource, /isTargetPathReady/);
    assert.match(restorePageSource, /expectedDistinguishedName/);
    assert.match(restorePageSource, /invalidateAdManagementDeletedObjectRestoreQueries/);
    assert.match(restorePageSource, /navigate\(returnPath\)/);
    assert.match(restorePageSource, /getAdManagementApiErrorMessage/);
  });

  it("shows not restorable state instead of form when ineligible", () => {
    assert.match(restorePageSource, /deletedObjects\.restore\.errors\.notRestorable/);
    assert.match(restorePageSource, /deletedObjects\.restore\.errors\.notRestorableDescription/);
  });

  it("uses constrained page container width", () => {
    assert.match(restorePageSource, /mx-auto w-full max-w-3xl space-y-4/);
  });
});

describe("deleted object detail layout", () => {
  const detailPageSource = readFileSync(
    new URL("./AdDeletedObjectDetailPage.tsx", import.meta.url),
    "utf8",
  );

  it("uses max-width container for detail states", () => {
    assert.match(detailPageSource, /mx-auto w-full max-w-7xl space-y-4/);
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

describe("deleted object restore i18n", () => {
  it("has parallel TR restore page keys", () => {
    assert.equal(trAdManagement.adManagement.deletedObjects.actions.restore, "Geri Yükle");
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.pageTitle,
      "Silinen nesneyi geri yükle",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.pageDescription,
      "Silinen AD nesnesini son bilinen konumuna geri yükleyin.",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.confirmLabel,
      "Geri yüklemek için sAMAccountName değerini yazın.",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.actions.submit,
      "Geri Yükle",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.confirmation.fallbackHint,
      "Devam etmek için nesne değerini yazın: {{value}}",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.confirmation.samAccountNameHint,
      "Devam etmek için sAMAccountName değerini yazın: {{value}}",
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
      trAdManagement.adManagement.deletedObjects.restore.targetMode.originalLocation,
      "Son bilinen konuma geri yükle",
    );
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.errors.targetPathRequired,
      "Farklı OU'ya geri yüklemek için hedef OU seçilmelidir.",
    );
  });

  it("has parallel EN restore page keys", () => {
    assert.equal(enAdManagement.adManagement.deletedObjects.actions.restore, "Restore");
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.pageTitle,
      "Restore deleted object",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.pageDescription,
      "Restore the deleted AD object to its last known location.",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.confirmLabel,
      "Type the sAMAccountName value to restore it.",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.actions.submit,
      "Restore",
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
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.targetMode.targetPath,
      "Restore to another OU",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.errors.targetPathRequired,
      "Select a target OU to restore to another location.",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.confirmation.samAccountNameHint,
      "Type the sAMAccountName value to continue: {{value}}",
    );
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.confirmation.fallbackHint,
      "Type the object value to continue: {{value}}",
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
