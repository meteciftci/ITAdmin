import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  AD_GROUP_SAM_ACCOUNT_NAME_MAX_LENGTH,
  buildAdGroupSamAccountNameSuggestion,
  normalizeAdGroupSamAccountNameSuggestion,
} from "./ad-group-name.ts";
import { normalizeAdUsername } from "./ad-user-name.ts";

describe("ad-group-name", () => {
  it("suggests samAccountName from technical name with Turkish normalization", () => {
    assert.equal(buildAdGroupSamAccountNameSuggestion("Şirket VPN"), "sirket.vpn");
  });

  it("does not truncate suggestions to 20 characters", () => {
    const technicalName = "very-long-group-technical-name-for-testing";
    const suggestion = normalizeAdGroupSamAccountNameSuggestion(technicalName);

    assert.ok(suggestion.length > 20);
    assert.equal(suggestion, technicalName);
  });

  it("uses a group-specific max length different from user username limit", () => {
    assert.equal(AD_GROUP_SAM_ACCOUNT_NAME_MAX_LENGTH, 64);
    assert.notEqual(AD_GROUP_SAM_ACCOUNT_NAME_MAX_LENGTH, 20);

    const longName = "abcdefghijklmnopqrstuvwxyz";
    assert.equal(normalizeAdGroupSamAccountNameSuggestion(longName).length, 26);
    assert.ok(normalizeAdUsername("abcdefghijklmnopqrstuvwxyz", "user").length <= 20);
  });
});
