import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";
import { readRouterSource } from "../../app/routes/route-source.test-support.ts";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");

test("DNS management routes are permission guarded", () => {
  const source = readRouterSource();
  assert.match(source, /path: "\/dns-management\/servers"/);
  assert.match(source, /DnsManagement\.Servers\.View/);
  assert.match(source, /path: "\/settings\/modules\/dns-management"/);
  assert.match(source, /DnsManagement\.ManageSettings/);
});

test("DNS credential response model exposes only password presence", () => {
  const source = readFileSync(join(root, "features/dns-management/types.ts"), "utf8");
  const response = source.slice(source.indexOf("export type DnsCredentialProfile"), source.indexOf("export type SaveDnsCredentialProfile"));
  assert.match(response, /hasPassword: boolean/);
  assert.doesNotMatch(response, /\n\s*password\??:/i);
});

test("DNS settings preserve the comparison refresh prompt", () => {
  const source = readFileSync(join(root, "features/settings/DnsManagementSettingsPage.tsx"), "utf8");
  assert.match(source, /promptForFullSyncOnComparisonOpen: true/);
  assert.match(source, /comparisonSnapshotStaleAfterMinutes/);
});

test("DNS connection test uses the dedicated endpoint and permission", () => {
  const api = readFileSync(join(root, "features/dns-management/api.ts"), "utf8");
  const page = readFileSync(join(root, "features/dns-management/DnsServersPage.tsx"), "utf8");
  assert.match(api, /servers\/\$\{id\}\/test-connection/);
  assert.match(page, /DnsManagement\.Servers\.TestConnection/);
  assert.match(page, /connectionResult\.capabilities/);
});

test("DNS locales have matching structures", () => {
  const tr = JSON.parse(readFileSync(join(root, "locales/tr/dnsManagement.json"), "utf8"));
  const en = JSON.parse(readFileSync(join(root, "locales/en/dnsManagement.json"), "utf8"));
  const keys = (value: unknown, prefix = ""): string[] => value && typeof value === "object"
    ? Object.entries(value).flatMap(([key, child]) => keys(child, `${prefix}.${key}`))
    : [prefix];
  assert.deepEqual(keys(tr).sort(), keys(en).sort());
});
