import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const page = readFileSync(new URL("./SystemUpdatesPage.tsx", import.meta.url), "utf8");
const routes = readFileSync(new URL("../../app/routes/settings-routes.tsx", import.meta.url), "utf8");
const tr = JSON.parse(readFileSync(new URL("../../locales/tr/systemUpdates.json", import.meta.url), "utf8"));
const en = JSON.parse(readFileSync(new URL("../../locales/en/systemUpdates.json", import.meta.url), "utf8"));

describe("system updates UI contract", () => {
  it("guards the route with the view permission", () => {
    assert.match(routes, /path: "\/settings\/updates"/);
    assert.match(routes, /PermissionCodes\.SystemUpdates\.View/);
  });

  it("requires backup confirmation and handles operator review", () => {
    assert.match(page, /backupConfirmed/);
    assert.match(page, /confirmDisabled=\{!backupConfirmed/);
    assert.match(page, /RequiresOperatorReview/);
  });

  it("keeps Turkish and English locale structures aligned", () => {
    const keys = (value: unknown, prefix = ""): string[] =>
      Object.entries(value as Record<string, unknown>).flatMap(([key, child]) => {
        const path = prefix ? `${prefix}.${key}` : key;
        return child && typeof child === "object" ? keys(child, path) : [path];
      });

    assert.deepEqual(keys(tr).sort(), keys(en).sort());
  });
});
