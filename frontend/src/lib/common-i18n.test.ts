import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

type CommonLocale = {
  common: {
    actions: Record<string, string>;
    status: Record<string, string>;
    dateRange: Record<string, string>;
    select: Record<string, string>;
    fields: Record<string, string>;
    channels: Record<string, string>;
  };
};

function readCommonLocale(language: "tr" | "en"): CommonLocale {
  return JSON.parse(
    readFileSync(new URL(`../locales/${language}/common.json`, import.meta.url), "utf8"),
  ) as CommonLocale;
}

function readSource(relativePath: string): string {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}

describe("common i18n locale keys", () => {
  const tr = readCommonLocale("tr");
  const en = readCommonLocale("en");

  const requiredActionKeys = [
    "clear",
    "detail",
    "edit",
    "save",
    "cancel",
    "refresh",
  ] as const;

  const requiredDateRangeKeys = ["placeholder", "clear"] as const;
  const requiredSelectKeys = ["searchOptions", "clearSelection", "noOptions"] as const;
  const requiredFieldKeys = [
    "name",
    "description",
    "status",
    "actions",
    "date",
    "code",
  ] as const;
  const requiredChannelKeys = ["sms", "email"] as const;

  for (const key of requiredActionKeys) {
    it(`defines common.actions.${key} in TR and EN`, () => {
      assert.ok(tr.common.actions[key], `missing tr common.actions.${key}`);
      assert.ok(en.common.actions[key], `missing en common.actions.${key}`);
    });
  }

  for (const key of requiredDateRangeKeys) {
    it(`defines common.dateRange.${key} in TR and EN`, () => {
      assert.ok(tr.common.dateRange[key], `missing tr common.dateRange.${key}`);
      assert.ok(en.common.dateRange[key], `missing en common.dateRange.${key}`);
    });
  }

  for (const key of requiredSelectKeys) {
    it(`defines common.select.${key} in TR and EN`, () => {
      assert.ok(tr.common.select[key], `missing tr common.select.${key}`);
      assert.ok(en.common.select[key], `missing en common.select.${key}`);
    });
  }

  for (const key of requiredFieldKeys) {
    it(`defines common.fields.${key} in TR and EN`, () => {
      assert.ok(tr.common.fields[key], `missing tr common.fields.${key}`);
      assert.ok(en.common.fields[key], `missing en common.fields.${key}`);
    });
  }

  for (const key of requiredChannelKeys) {
    it(`defines common.channels.${key} in TR and EN`, () => {
      assert.ok(tr.common.channels[key], `missing tr common.channels.${key}`);
      assert.ok(en.common.channels[key], `missing en common.channels.${key}`);
    });
  }

  it("keeps TR and EN common key structures aligned for new sections", () => {
    assert.deepEqual(Object.keys(tr.common.dateRange).sort(), Object.keys(en.common.dateRange).sort());
    assert.deepEqual(Object.keys(tr.common.select).sort(), Object.keys(en.common.select).sort());
    assert.deepEqual(Object.keys(tr.common.fields).sort(), Object.keys(en.common.fields).sort());
    assert.deepEqual(Object.keys(tr.common.channels).sort(), Object.keys(en.common.channels).sort());
  });
});

describe("common i18n component usage", () => {
  const logPages = [
    "../features/audit-logs/AuditLogsPage.tsx",
    "../features/security-logs/SecurityLogsPage.tsx",
    "../features/ad-management/AdOperationLogsPage.tsx",
  ] as const;

  for (const pagePath of logPages) {
    it(`uses common dateRange keys in ${pagePath}`, () => {
      const source = readSource(pagePath);
      assert.match(source, /common:dateRange\.placeholder/);
      assert.match(source, /common:dateRange\.clear/);
      assert.doesNotMatch(source, /filters\.dateRangePlaceholder/);
      assert.doesNotMatch(source, /filters\.clearDateRange/);
    });
  }

  it("uses common clear label in account expiration date picker", () => {
    const source = readSource(
      "../features/ad-management/components/ad-user-detail/AdUserAccountExpirationSection.tsx",
    );

    assert.match(source, /clearLabel=\{t\("common:actions\.clear"\)\}/);
    assert.doesNotMatch(source, /clearLabel=\{t\("actions\.clear"\)\}/);
  });
});
