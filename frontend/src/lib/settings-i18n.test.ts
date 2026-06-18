import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

type JsonValue = string | number | boolean | null | JsonObject | JsonValue[];
type JsonObject = { [key: string]: JsonValue };

function readSettingsLocale(language: "tr" | "en"): JsonObject {
  const parsed = JSON.parse(
    readFileSync(new URL(`../locales/${language}/settings.json`, import.meta.url), "utf8"),
  ) as { settings: JsonObject };

  return parsed.settings;
}

function flattenKeys(obj: JsonObject, prefix = ""): string[] {
  const keys: string[] = [];

  for (const [key, value] of Object.entries(obj)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value && typeof value === "object" && !Array.isArray(value)) {
      keys.push(...flattenKeys(value as JsonObject, path));
      continue;
    }

    keys.push(path);
  }

  return keys;
}

describe("settings i18n locale parity", () => {
  const tr = readSettingsLocale("tr");
  const en = readSettingsLocale("en");
  const trKeys = flattenKeys(tr).sort();
  const enKeys = flattenKeys(en).sort();

  it("keeps TR and EN settings key structures aligned", () => {
    assert.deepEqual(trKeys, enKeys);
  });

  it("does not contain typo logicalField Help key", () => {
    assert.equal(
      trKeys.some((key) => key.includes("logicalField Help")),
      false,
      "typo key logicalField Help must be removed",
    );
  });
});
