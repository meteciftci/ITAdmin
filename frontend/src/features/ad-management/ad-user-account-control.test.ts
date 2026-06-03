import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  formatAdUserAccountControlValue,
  parseAdUserAccountControlFlags,
} from "./ad-user-account-control.ts";

describe("parseAdUserAccountControlFlags", () => {
  it("parses 512 as NORMAL_ACCOUNT", () => {
    const parsed = parseAdUserAccountControlFlags(512);
    assert.equal(parsed.rawValue, 512);
    assert.deepEqual(parsed.flags, ["NORMAL_ACCOUNT"]);
    assert.equal(parsed.unknownMask, 0);
  });

  it("parses 514 as ACCOUNTDISABLE and NORMAL_ACCOUNT", () => {
    const parsed = parseAdUserAccountControlFlags(514);
    assert.deepEqual(parsed.flags, ["ACCOUNTDISABLE", "NORMAL_ACCOUNT"]);
  });

  it("parses 66048 as NORMAL_ACCOUNT and DONT_EXPIRE_PASSWORD", () => {
    const parsed = parseAdUserAccountControlFlags(66048);
    assert.deepEqual(parsed.flags, ["NORMAL_ACCOUNT", "DONT_EXPIRE_PASSWORD"]);
  });

  it("returns empty flags for null", () => {
    const parsed = parseAdUserAccountControlFlags(null);
    assert.equal(parsed.rawValue, null);
    assert.deepEqual(parsed.flags, []);
  });
});

describe("formatAdUserAccountControlValue", () => {
  it("formats numeric values and dash for null", () => {
    assert.equal(formatAdUserAccountControlValue(512), "512");
    assert.equal(formatAdUserAccountControlValue(null), "-");
  });
});
