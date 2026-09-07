import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const page = readFileSync(new URL("./SystemHttpsPage.tsx", import.meta.url), "utf8");
const api = readFileSync(new URL("./api.ts", import.meta.url), "utf8");
const routes = readFileSync(new URL("../../app/routes/settings-routes.tsx", import.meta.url), "utf8");
const sidebar = readFileSync(new URL("../../components/layout/sidebar-items.ts", import.meta.url), "utf8");
const tr = JSON.parse(readFileSync(new URL("../../locales/tr/systemHttps.json", import.meta.url), "utf8"));
const en = JSON.parse(readFileSync(new URL("../../locales/en/systemHttps.json", import.meta.url), "utf8"));

describe("system HTTPS UI contract", () => {
  it("guards the route and the sidebar entry with the view permission", () => {
    assert.match(routes, /path: "\/settings\/https"/);
    assert.match(routes, /PermissionCodes\.SystemHttps\.View/);
    assert.match(sidebar, /"\/settings\/https"/);
    assert.match(sidebar, /PermissionCodes\.SystemHttps\.View/);
  });

  it("uploads the PFX as multipart form data and can disable HTTPS", () => {
    assert.match(api, /new FormData\(\)/);
    assert.match(api, /form\.append\("pfx"/);
    assert.match(api, /multipart\/form-data/);
    assert.match(api, /\/system\/https\/disable/);
  });

  it("gates the upload form and the disable action behind Manage", () => {
    assert.match(page, /PermissionCodes\.SystemHttps\.Manage/);
    assert.match(page, /accept="\.pfx/);
    assert.match(page, /disable\.confirmTitle/);
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
