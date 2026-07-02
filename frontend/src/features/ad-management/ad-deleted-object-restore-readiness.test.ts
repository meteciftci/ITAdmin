import assert from "node:assert/strict";
import { readAdManagementApiSource } from "./api/api-source.test-support.ts";
import { readFileSync, readdirSync } from "node:fs";
import { describe, it } from "node:test";

import trAdManagement from "../../locales/tr/adManagement.json" with { type: "json" };
import enAdManagement from "../../locales/en/adManagement.json" with { type: "json" };
import { translateReadinessText } from "./restore-readiness-i18n.ts";

describe("deleted object restore readiness API", () => {
  const apiSource = readAdManagementApiSource();
  // The AD management types are split across ./types/*.ts (re-exported by types.ts).
  const typesDir = new URL("./types/", import.meta.url);
  const typesSource = readdirSync(typesDir)
    .filter((file) => file.endsWith(".ts"))
    .map((file) => readFileSync(new URL(file, typesDir), "utf8"))
    .join("\n");

  it("defines readiness query key and API functions", () => {
    assert.match(apiSource, /AD_MANAGEMENT_DELETED_OBJECT_RESTORE_READINESS_QUERY_KEY/);
    assert.match(apiSource, /getAdDeletedObjectRestoreReadiness/);
    assert.match(apiSource, /\/ad-management\/deleted-objects\/restore-readiness/);
    assert.match(apiSource, /validateAdManagementSettings/);
    assert.match(apiSource, /\/ad-management\/settings\/validate/);
  });

  it("defines readiness result types with key and params fields", () => {
    assert.match(typesSource, /AdDeletedObjectRestoreReadinessResult/);
    assert.match(typesSource, /AdDeletedObjectRestoreReadinessCheck/);
    assert.match(typesSource, /summaryKey/);
    assert.match(typesSource, /titleKey/);
    assert.match(typesSource, /messageKey/);
    assert.match(typesSource, /remediationKey/);
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

  it("avoids duplicate warning checks in checks list", () => {
    assert.match(panelSource, /warningKeys/);
    assert.match(panelSource, /!warningKeys\.has\(check\.key\)/);
  });

  it("renders summary and check title through readiness i18n helper", () => {
    assert.match(panelSource, /translateReadinessText/);
    assert.match(panelSource, /result\.summaryKey/);
    assert.match(panelSource, /check\.titleKey/);
    assert.doesNotMatch(panelSource, /\{check\.title\}/);
    assert.doesNotMatch(panelSource, /\{result\.summaryMessage\}/);
  });

  it("does not translate command field", () => {
    assert.match(panelSource, /<code className="block flex-1[\s\S]*\{check\.command\}/);
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
    assert.match(cardSource, /showRetry={false}/);
    assert.doesNotMatch(cardSource, /validateAdManagementSettings/);
  });
});

describe("deleted object restore readiness i18n", () => {
  const readiness = trAdManagement.adManagement.deletedObjects.restore.readiness;

  it("has parallel TR readiness keys", () => {
    assert.equal(
      readiness.unavailableTitle,
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
    assert.equal(readiness.summary.ready, "Geri yükleme gereksinimleri karşılanıyor.");
    assert.equal(
      readiness.checks.restorePermissionVerification.verified,
      "Geri yükleme yetkisi başarılı restore işlem logu ile doğrulandı.",
    );
    assert.equal(readiness.checkStatus.notChecked, "Bilgi");
    assert.equal(
      readiness.checks.adwsPortConnectivity.success,
      "{{host}}:{{port}} bağlantısı başarılı.",
    );
  });

  it("has parallel EN readiness keys", () => {
    const enReadiness = enAdManagement.adManagement.deletedObjects.restore.readiness;

    assert.equal(
      enReadiness.unavailableTitle,
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
    assert.equal(enReadiness.summary.ready, "Restore prerequisites are met.");
    assert.equal(
      enReadiness.checks.restorePermissionVerification.notVerified,
      "Restore permission has not yet been verified by a successful restore operation log.",
    );
    assert.equal(enReadiness.checkStatus.notChecked, "Info");
    assert.equal(
      enReadiness.checks.powerShellTimeout.success,
      "PowerShellTimeoutSeconds is appropriate ({{configuredTimeoutSeconds}} seconds).",
    );
  });

  it("interpolates readiness params through helper", () => {
    const t = ((key: string, params?: Record<string, unknown>) => {
      if (key === "adManagement:deletedObjects.restore.readiness.checks.adwsPortConnectivity.success") {
        return `Connection to ${params?.host}:${params?.port} succeeded.`;
      }
      return key;
    }) as never;

    const text = translateReadinessText(
      t,
      "deletedObjects.restore.readiness.checks.adwsPortConnectivity.success",
      { host: "dc1.muglabb.lcl", port: 9389 },
    );

    assert.equal(text, "Connection to dc1.muglabb.lcl:9389 succeeded.");
  });
});
