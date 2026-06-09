import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const appLayoutSource = readFileSync(
  new URL("./AppLayout.tsx", import.meta.url),
  "utf8",
);

describe("AppLayout scroll container", () => {
  it("marks main content area as the app scroll container", () => {
    assert.match(appLayoutSource, /data-app-scroll-container="true"/);
    assert.match(appLayoutSource, /<main[\s\S]*overflow-y-auto/);
  });
});
