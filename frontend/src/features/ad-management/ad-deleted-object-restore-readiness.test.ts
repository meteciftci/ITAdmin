import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import trAdManagement from "../../locales/tr/adManagement.json" with { type: "json" };
import enAdManagement from "../../locales/en/adManagement.json" with { type: "json" };

describe("deleted object restore readiness API", () => {
  const apiSource = readFileSync(new URL("./api.ts", import.meta.url), "utf8");
  const typesSource = readFileSync(new URL("./types.ts", import.meta.url), "utf8");

  it("defines readiness query key and API functions", () => {
    assert.match(apiSource, /AD_MANAGEMENT_DELETED_OBJECT_RESTORE_READINESS_QUERY_KEY/);
    assert.match(apiSource, /getAdDeletedObjectRestoreReadiness/);
    assert.match(apiSource, /\/ad-management\/deleted-objects\/restore-readiness/);
    assert.match(apiSource, /validateAdManagementSettings/);
    assert.match(apiSource, /\/ad-management\/settings\/validate/);
  });

  it("defines readiness result types and validation restoreReadiness field", () => {
    assert.match(typesSource, /AdDeletedObjectRestoreReadinessResult/);
    assert.match(typesSource, /AdDeletedObjectRestoreReadinessCheck/);
    assert.match(typesSource, /restoreReadiness\?:/);
  });
});

describe("deleted object restore readiness page", () => {
  const restorePageSource = readFileSync(
    new URL("./AdDeletedObjectRestorePage.tsx", import.meta.url),
    "utf8",
  );
  const panelSource = readFileSync(
    new URL("./components/AdDeletedObjectRestoreReadinessPanel.tsx", import.meta.url),
    "utf8",
  );

  it("loads readiness and hides form when not ready", () => {
    assert.match(restorePageSource, /getAdDeletedObjectRestoreReadiness/);
    assert.match(restorePageSource, /AD_MANAGEMENT_DELETED_OBJECT_RESTORE_READINESS_QUERY_KEY/);
    assert.match(restorePageSource, /AdDeletedObjectRestoreReadinessPanel/);
    assert.match(restorePageSource, /!readiness\.isReady/);
    assert.match(restorePageSource, /canShowRestoreForm/);
    assert.match(restorePageSource, /readiness\.isReady/);
  });

  it("shows readiness error state with retry", () => {
    assert.match(restorePageSource, /readinessQuery\.isError/);
    assert.match(restorePageSource, /verifyFailedTitle/);
    assert.match(restorePageSource, /readinessQuery\.refetch/);
  });

  it("shows warning panel and form together when ready with warnings", () => {
    assert.match(restorePageSource, /canShowReadinessWarningBanner/);
    assert.match(restorePageSource, /readiness\.status === "Warning"/);
  });

  it("renders blocking reasons, remediation and copyable command", () => {
    assert.match(panelSource, /blockingReasons/);
    assert.match(panelSource, /remediation/);
    assert.match(panelSource, /navigator\.clipboard\.writeText/);
    assert.match(panelSource, /<code/);
  });
});

describe("settings restore readiness card", () => {
  const settingsTabSource = readFileSync(
    new URL("./AdManagementSettingsTab.tsx", import.meta.url),
    "utf8",
  );
  const cardSource = readFileSync(
    new URL("./components/AdDeletedObjectRestoreReadinessCard.tsx", import.meta.url),
    "utf8",
  );

  it("renders restore readiness card on settings connection tab", () => {
    assert.match(settingsTabSource, /AdDeletedObjectRestoreReadinessCard/);
    assert.match(cardSource, /settings\.restoreReadiness\.title/);
    assert.match(cardSource, /settings\.restoreReadiness\.check/);
    assert.match(cardSource, /validateAdManagementSettings/);
  });
});

describe("deleted object restore readiness i18n", () => {
  it("has parallel TR readiness keys", () => {
    assert.equal(
      trAdManagement.adManagement.deletedObjects.restore.readiness.unavailableTitle,
      "Silinen nesne geri yükleme özelliği şu anda kullanılamıyor.",
    );
    assert.equal(
      trAdManagement.adManagement.settings.restoreReadiness.title,
      "Silinen Nesne Geri Yükleme Gereksinimleri",
    );
    assert.equal(
      trAdManagement.adManagement.settings.restoreReadiness.check,
      "Gereksinimleri Kontrol Et",
    );
  });

  it("has parallel EN readiness keys", () => {
    assert.equal(
      enAdManagement.adManagement.deletedObjects.restore.readiness.unavailableTitle,
      "Deleted object restore is currently unavailable.",
    );
    assert.equal(
      enAdManagement.adManagement.settings.restoreReadiness.title,
      "Deleted Object Restore Prerequisites",
    );
    assert.equal(
      enAdManagement.adManagement.settings.restoreReadiness.check,
      "Check Prerequisites",
    );
  });
});
