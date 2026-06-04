import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  getAdGroupPathNodeLabel,
  getAdGroupPrimaryLabel,
  getAdGroupSecondaryLabel,
} from "./ad-group-display-labels.ts";

describe("ad-group-display-labels", () => {
  it("uses displayName as primary label when available", () => {
    const primary = getAdGroupPrimaryLabel({
      displayName: "VPN Users",
      name: "VPN_Users",
      samAccountName: "VPN_Users",
      distinguishedName: "CN=VPN_Users,OU=Groups,DC=example,DC=com",
    });

    assert.equal(primary, "VPN Users");
  });

  it("does not repeat name as secondary when it matches displayName", () => {
    const group = {
      displayName: "VPN Users",
      name: "VPN Users",
      samAccountName: "VPN_Users",
      distinguishedName: "CN=VPN_Users,OU=Groups,DC=example,DC=com",
    };
    const primary = getAdGroupPrimaryLabel(group);
    const secondary = getAdGroupSecondaryLabel(group, primary);

    assert.equal(secondary, "VPN_Users");
  });

  it("returns null secondary when all candidates match primary", () => {
    const group = {
      displayName: "Same Label",
      name: "Same Label",
      samAccountName: "Same Label",
      description: "Same Label",
      distinguishedName: "CN=Same,OU=Groups,DC=example,DC=com",
    };
    const primary = getAdGroupPrimaryLabel(group);

    assert.equal(getAdGroupSecondaryLabel(group, primary), null);
  });

  it("prefers path node displayName over name and samAccountName", () => {
    const label = getAdGroupPathNodeLabel({
      displayName: "Mete TEST",
      name: "mete.test",
      samAccountName: "mete.test",
      distinguishedName: "CN=Mete TEST,OU=Users,DC=example,DC=com",
    });

    assert.equal(label, "Mete TEST");
  });
});
