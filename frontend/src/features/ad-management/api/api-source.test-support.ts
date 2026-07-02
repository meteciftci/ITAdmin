import { readFileSync } from "node:fs";

/**
 * Reads and concatenates the source of every AD-management api submodule plus the api
 * barrel. Used by navigation/wiring tests that assert endpoint paths, query-key names,
 * and function wiring by scanning source text; the api surface is split across domain
 * modules, so a single concatenated view keeps those assertions stable regardless of
 * which module a given function lives in.
 */
export function readAdManagementApiSource(): string {
  const moduleUrls = [
    new URL("../api.ts", import.meta.url),
    new URL("./query-keys.ts", import.meta.url),
    new URL("./settings-api.ts", import.meta.url),
    new URL("./users-api.ts", import.meta.url),
    new URL("./groups-api.ts", import.meta.url),
    new URL("./computers-api.ts", import.meta.url),
    new URL("./organizational-units-api.ts", import.meta.url),
    new URL("./deleted-objects-api.ts", import.meta.url),
  ];

  return moduleUrls.map((url) => readFileSync(url, "utf8")).join("\n");
}
