import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, it } from "node:test";

const currentDir = dirname(fileURLToPath(import.meta.url));

function readSource(relativePath: string): string {
  return readFileSync(join(currentDir, relativePath), "utf8");
}

describe("AdManagementNotificationsForm provider readiness", () => {
  it("loads SMS and email provider settings with shared query keys", () => {
    const source = readSource("components/AdManagementNotificationsForm.tsx");

    assert.match(source, /getSmsProviderSettings/);
    assert.match(source, /getEmailProviderSettings/);
    assert.match(source, /NOTIFICATION_SMS_SETTINGS_QUERY_KEY/);
    assert.match(source, /NOTIFICATION_EMAIL_SETTINGS_QUERY_KEY/);
    assert.match(source, /retry:\s*false/);
  });

  it("returns BlockingStateCard while provider readiness is loading", () => {
    const source = readSource("components/AdManagementNotificationsForm.tsx");

    assert.match(source, /if \(providerReadinessLoading\)/);
    assert.match(source, /BlockingStateCard/);
    assert.match(source, /providerReadiness\.title/);
    assert.match(source, /providerReadiness\.description/);
    assert.doesNotMatch(
      source.slice(source.indexOf("if (providerReadinessLoading)"), source.indexOf("if (showProviderWarning)")),
      /DataTableToolbar/,
    );
  });

  it("returns BlockingStateCard when no provider is ready", () => {
    const source = readSource("components/AdManagementNotificationsForm.tsx");

    assert.match(source, /if \(showProviderWarning\)/);
    assert.match(source, /providerMissing\.title/);
    assert.match(source, /providerMissing\.accessNote/);
    assert.match(source, /providerMissing\.hint/);

    const warningBranch = source.slice(
      source.indexOf("if (showProviderWarning)"),
      source.indexOf("return (\n    <div className=\"space-y-4\">"),
    );
    assert.doesNotMatch(warningBranch, /DataTableToolbar/);
    assert.doesNotMatch(warningBranch, /EmptyState/);
    assert.doesNotMatch(warningBranch, /AdManagementNotificationRuleDialog/);
  });

  it("guards rule mutations when channel provider is not ready", () => {
    const source = readSource("components/AdManagementNotificationsForm.tsx");

    assert.match(source, /guardRuleMutation/);
    assert.match(source, /messages\.providerNotReady/);
    assert.match(source, /handleToggleEnabled/);
    assert.match(source, /handleRemove/);
    assert.match(source, /handleRuleSubmit/);
  });

  it("renders notification rules UI when providers are ready", () => {
    const source = readSource("components/AdManagementNotificationsForm.tsx");
    const readyBranch = source.slice(source.lastIndexOf('return (\n    <div className="space-y-4">'));

    assert.match(readyBranch, /DataTableToolbar/);
    assert.match(readyBranch, /DataTable/);
    assert.match(readyBranch, /AdManagementNotificationRuleDialog/);
    assert.match(readyBranch, /handleOpenCreateDialog/);
  });
});

describe("AdManagementNotificationRuleDialog channel readiness", () => {
  it("restricts channel options based on provider readiness", () => {
    const source = readSource("components/AdManagementNotificationRuleDialog.tsx");

    assert.match(source, /channelReadiness/);
    assert.match(source, /disabled=\{!channelReadiness\.sms\}/);
    assert.match(source, /disabled=\{!channelReadiness\.email\}/);
    assert.match(source, /validation\.channelUnavailable/);
    assert.match(source, /isAdNotificationChannelReady/);
  });
});

describe("ad notification rule columns mutation guard", () => {
  it("disables row actions when rules cannot be mutated", () => {
    const source = readSource("ad-notification-rule-columns.tsx");

    assert.match(source, /canMutateRules/);
    assert.match(source, /isRuleMutable/);
    assert.match(source, /isActionDisabled/);
  });
});

describe("ad management notifications i18n", () => {
  it("defines provider readiness keys in Turkish and English", () => {
    const tr = readFileSync(
      join(currentDir, "../../locales/tr/settings.json"),
      "utf8",
    );
    const en = readFileSync(
      join(currentDir, "../../locales/en/settings.json"),
      "utf8",
    );

    for (const key of [
      "providerMissing",
      "providerReadiness",
      "accessNote",
      "providerNotReady",
      "channelUnavailable",
    ]) {
      assert.match(tr, new RegExp(key));
      assert.match(en, new RegExp(key));
    }
  });
});
