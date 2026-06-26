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
  it("exposes variant, size, width, centered and accessibility props", () => {
    const source = readSource("BlockingStateCard.tsx");

    assert.match(source, /BlockingStateCardVariant/);
    assert.match(source, /BlockingStateCardSize/);
    assert.match(source, /BlockingStateCardWidth/);
    assert.match(source, /width = "contained"/);
    assert.match(source, /centered\?: boolean/);
    assert.match(source, /role="alert"/);
    assert.match(source, /aria-live/);
    assert.match(source, /border-border\/80 bg-card shadow-md/);
  });

  it("keeps contained max width classes for default width", () => {
    const source = readSource("BlockingStateCard.tsx");

    assert.match(source, /max-w-lg/);
    assert.match(source, /max-w-2xl/);
    assert.match(source, /!isFullWidth && "justify-center"/);
  });

  it("uses full panel width when width is full", () => {
    const source = readSource("BlockingStateCard.tsx");

    assert.match(source, /isFullWidth[\s\S]*max-w-none/);
    assert.doesNotMatch(
      source.slice(source.indexOf('width === "full"'), source.indexOf("return (")),
      /max-w-lg/,
    );
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
