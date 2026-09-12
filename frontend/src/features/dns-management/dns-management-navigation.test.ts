import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";
import { readRouterSource } from "../../app/routes/route-source.test-support.ts";
import { buildDnsZoneRecordsPath } from "./dns-inventory-paths.ts";
import { formatDnsRecordValue } from "./dns-record-value.ts";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");

test("DNS management routes are permission guarded", () => {
  const source = readRouterSource();
  const redirect = readFileSync(join(root, "features/dns-management/DnsManagementRedirectPage.tsx"), "utf8");
  assert.match(source, /path: "\/dns-management\/servers"/);
  assert.match(source, /DnsManagement\.Servers\.View/);
  assert.match(source, /DnsManagementRedirectPage/);
  assert.match(redirect, /DnsManagement\.Servers\.View/);
  assert.match(redirect, /DnsManagement\.Zones\.View/);
  assert.match(redirect, /DnsManagement\.Records\.View/);
  assert.match(redirect, /getErrorRoutePath\("FORBIDDEN"\)/);
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

test("DNS inventory synchronization is queued with dedicated permission and status polling", () => {
  const api = readFileSync(join(root, "features/dns-management/api.ts"), "utf8");
  const page = readFileSync(join(root, "features/dns-management/DnsServersPage.tsx"), "utf8");
  assert.match(api, /servers\/\$\{id\}\/synchronizations/);
  assert.match(page, /DnsManagement\.Synchronize/);
  assert.match(page, /refetchInterval/);
  assert.match(page, /lastSuccessfulSyncAt/);
});

test("DNS inventory routes and API are permission guarded database snapshot reads", () => {
  const routes = readRouterSource();
  const api = readFileSync(join(root, "features/dns-management/api.ts"), "utf8");
  const zonesPage = readFileSync(join(root, "features/dns-management/DnsZonesPage.tsx"), "utf8");
  const recordsPage = readFileSync(join(root, "features/dns-management/DnsZoneRecordsPage.tsx"), "utf8");
  assert.match(routes, /path: "\/dns-management\/zones"/);
  assert.match(routes, /DnsManagement\.Zones\.View/);
  assert.match(routes, /DnsManagement\.Records\.View/);
  assert.match(api, /inventory\/zones/);
  assert.match(zonesPage, /DataTablePagination/);
  assert.match(recordsPage, /DataTablePagination/);
  assert.equal(buildDnsZoneRecordsPath("zone id"), "/dns-management/zones/zone%20id/records");
});

test("DNS comparison uses cached snapshots, guarded routes, and a once-per-session refresh prompt", () => {
  const routes = readRouterSource();
  const api = readFileSync(join(root, "features/dns-management/api.ts"), "utf8");
  const page = readFileSync(join(root, "features/dns-management/DnsComparisonPage.tsx"), "utf8");
  const columns = readFileSync(join(root, "features/dns-management/dns-comparison-columns.tsx"), "utf8");
  assert.match(routes, /path: "\/dns-management\/comparison"/);
  assert.match(routes, /DnsManagement\.Compare/);
  assert.match(api, /inventory\/comparison\/query/);
  assert.match(api, /inventory\/comparison\/synchronizations/);
  assert.match(page, /sessionStorage/);
  assert.match(page, /MultiSelectFilter/);
  assert.match(page, /lastFullInventorySyncAt/);
  assert.match(page, /snapshotScope === "FullInventory"/);
  assert.match(page, /DNS_COMPARISON_RESULTS_QUERY_KEY/);
  assert.match(page, /DnsManagement\.Synchronize/);
  assert.match(columns, /comparison\.status/);
});

test("DNS record values are rendered as readable structured text", () => {
  assert.equal(
    formatDnsRecordValue('{"IPv4Address":"10.0.0.10"}'),
    "IPv4Address: 10.0.0.10",
  );
  assert.equal(formatDnsRecordValue("plain value"), "plain value");
});

test("DNS record mutations are typed, permission-aware, confirmed, and refresh cached inventory", () => {
  const api = readFileSync(join(root, "features/dns-management/api.ts"), "utf8");
  const page = readFileSync(join(root, "features/dns-management/DnsZoneRecordsPage.tsx"), "utf8");
  const zonesPage = readFileSync(join(root, "features/dns-management/DnsZonesPage.tsx"), "utf8");
  const dialog = readFileSync(join(root, "features/dns-management/DnsRecordDialog.tsx"), "utf8");
  assert.match(api, /expectedRecordHash: record\.recordHash/);
  assert.match(api, /apiClient\.delete<DnsRecordMutation>/);
  assert.match(page, /DnsManagement\.Records\.Create/);
  assert.match(page, /DnsManagement\.Records\.Update/);
  assert.match(page, /DnsManagement\.Records\.Delete/);
  assert.match(page, /ConfirmDialog/);
  assert.match(page, /invalidateQueries\(\{ queryKey: \["dns-management", "inventory"\]/);
  assert.match(page, /navigate\(DNS_ZONES_PATH\)/);
  assert.match(zonesPage, /refetchInterval/);
  assert.match(zonesPage, /snapshotKey/);
  assert.match(dialog, /writableDnsRecordTypes/);
  assert.doesNotMatch(api, /power\s*shell/i);
});

test("DNS locales have matching structures", () => {
  const tr = JSON.parse(readFileSync(join(root, "locales/tr/dnsManagement.json"), "utf8"));
  const en = JSON.parse(readFileSync(join(root, "locales/en/dnsManagement.json"), "utf8"));
  const keys = (value: unknown, prefix = ""): string[] => value && typeof value === "object"
    ? Object.entries(value).flatMap(([key, child]) => keys(child, `${prefix}.${key}`))
    : [prefix];
  assert.deepEqual(keys(tr).sort(), keys(en).sort());
});
