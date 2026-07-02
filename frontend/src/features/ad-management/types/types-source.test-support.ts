import { readFileSync } from "node:fs";

/**
 * Reads and concatenates the source of every AD-management types submodule plus the types
 * barrel. Used by tests that assert type declarations exist by scanning source text; the
 * type surface is split across domain modules, so a single concatenated view keeps those
 * assertions stable regardless of which module a given type lives in.
 */
export function readAdManagementTypesSource(): string {
  const moduleUrls = [
    new URL("../types.ts", import.meta.url),
    new URL("./common.ts", import.meta.url),
    new URL("./users.ts", import.meta.url),
    new URL("./groups.ts", import.meta.url),
    new URL("./computers.ts", import.meta.url),
    new URL("./organizational-units.ts", import.meta.url),
    new URL("./deleted-objects.ts", import.meta.url),
  ];

  return moduleUrls.map((url) => readFileSync(url, "utf8")).join("\n");
}
