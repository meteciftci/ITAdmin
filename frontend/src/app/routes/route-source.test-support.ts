import { readFileSync } from "node:fs";

/**
 * Reads and concatenates the source of every route-tree module plus the composing
 * router entry point. Used by navigation tests that assert route paths and permission
 * wiring by scanning source text; the route tree is split across feature modules, so a
 * single concatenated view keeps those assertions stable regardless of which module a
 * given route lives in.
 */
export function readRouterSource(): string {
  const moduleUrls = [
    new URL("../router.tsx", import.meta.url),
    new URL("../lazy-pages.ts", import.meta.url),
    new URL("./core-routes.tsx", import.meta.url),
    new URL("./settings-routes.tsx", import.meta.url),
    new URL("./ad-management-routes.tsx", import.meta.url),
    new URL("./license-management-routes.tsx", import.meta.url),
  ];

  return moduleUrls.map((url) => readFileSync(url, "utf8")).join("\n");
}
