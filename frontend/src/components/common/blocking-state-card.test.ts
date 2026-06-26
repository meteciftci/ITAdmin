import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, it } from "node:test";

const currentDir = dirname(fileURLToPath(import.meta.url));

function readSource(relativePath: string): string {
  return readFileSync(join(currentDir, relativePath), "utf8");
}

describe("BlockingStateCard", () => {
  it("exposes variant, size, centered and accessibility props", () => {
    const source = readSource("BlockingStateCard.tsx");

    assert.match(source, /BlockingStateCardVariant/);
    assert.match(source, /BlockingStateCardSize/);
    assert.match(source, /centered\?: boolean/);
    assert.match(source, /role="alert"/);
    assert.match(source, /aria-live/);
    assert.match(source, /border-border\/80 bg-card shadow-md/);
  });
});

describe("ServiceUnavailableState", () => {
  it("renders through BlockingStateCard with readiness details and retry", () => {
    const source = readSource("ServiceUnavailableState.tsx");

    assert.match(source, /BlockingStateCard/);
    assert.match(source, /variant="danger"/);
    assert.match(source, /serviceUnavailable\.apiUnavailable/);
    assert.match(source, /serviceUnavailable\.databaseUnavailable/);
    assert.match(source, /serviceUnavailable\.ldapUnavailable/);
    assert.match(source, /serviceUnavailable\.lastChecked/);
    assert.match(source, /serviceUnavailable\.retry/);
    assert.match(source, /disabled=\{isLoading\}/);
    assert.match(source, /isLoading && "animate-spin"/);
  });
});
