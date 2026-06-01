import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { parseJsonLikeValue, unwrapJsonLikeString } from "./parse-json-like-value.ts";

describe("parseJsonLikeValue", () => {
  it("returns empty for nullish values", () => {
    assert.deepEqual(parseJsonLikeValue(null), { kind: "empty" });
    assert.deepEqual(parseJsonLikeValue(undefined), { kind: "empty" });
    assert.deepEqual(parseJsonLikeValue("   "), { kind: "empty" });
  });

  it("pretty prints a normal JSON string", () => {
    const input = JSON.stringify({ givenName: "Ali", surname: "Veli" });
    const result = parseJsonLikeValue(input);

    assert.equal(result.kind, "pretty");
    if (result.kind === "pretty") {
      assert.match(result.text, /"givenName": "Ali"/);
      assert.match(result.text, /"surname": "Veli"/);
      assert.ok(result.text.includes("\n"));
    }
  });

  it("pretty prints double-encoded JSON strings", () => {
    const inner = JSON.stringify({ givenName: "Ali", surname: "Veli" });
    const doubleEncoded = JSON.stringify(inner);
    const result = parseJsonLikeValue(doubleEncoded);

    assert.equal(result.kind, "pretty");
    if (result.kind === "pretty") {
      assert.match(result.text, /"givenName": "Ali"/);
    }
  });

  it("pretty prints object input", () => {
    const result = parseJsonLikeValue({ a: 1, b: [2, 3] });
    assert.equal(result.kind, "pretty");
    if (result.kind === "pretty") {
      assert.match(result.text, /"a": 1/);
      assert.match(result.text, /"b":/);
    }
  });

  it("returns raw text for invalid JSON", () => {
    const result = parseJsonLikeValue("not json");
    assert.deepEqual(result, { kind: "raw", text: "not json" });
  });
});

describe("unwrapJsonLikeString", () => {
  it("unwraps nested JSON strings", () => {
    const inner = JSON.stringify({ changeStatus: "NoChangesDetected" });
    const wrapped = JSON.stringify(inner);
    const unwrapped = unwrapJsonLikeString(wrapped);

    assert.deepEqual(unwrapped, { changeStatus: "NoChangesDetected" });
  });
});
