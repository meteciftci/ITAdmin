import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  getAdGroupMemberPrimaryLabel,
  getAdGroupMemberSecondaryLabel,
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
      cn: "Same Label",
      samAccountName: "Same Label",
      distinguishedName: "CN=Same,OU=Groups,DC=example,DC=com",
    };
    const primary = getAdGroupPrimaryLabel(group);

    assert.equal(getAdGroupSecondaryLabel(group, primary), null);
  });

  it("uses cn in primary label fallback before samAccountName", () => {
    const primary = getAdGroupPrimaryLabel({
      displayName: null,
      name: null,
      cn: "VPN Users",
      samAccountName: "vpn-users",
      distinguishedName: "CN=VPN Users,OU=Groups,DC=example,DC=com",
    });

    assert.equal(primary, "VPN Users");
  });

  it("prefers samAccountName over name for secondary label", () => {
    const group = {
      displayName: "VPN Users",
      name: "VPN Users Name",
      cn: "VPN Users",
      samAccountName: "vpn-users",
      distinguishedName: "CN=VPN Users,OU=Groups,DC=example,DC=com",
    };
    const primary = getAdGroupPrimaryLabel(group);
    const secondary = getAdGroupSecondaryLabel(group, primary);

    assert.equal(secondary, "vpn-users");
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

  it("uses displayName as member primary label", () => {
    const member = {
      displayName: "John Doe",
      name: "john.doe",
      samAccountName: "john.doe",
      distinguishedName: "CN=John Doe,OU=Users,DC=example,DC=com",
      description: "Test user",
    };

    assert.equal(getAdGroupMemberPrimaryLabel(member), "John Doe");
  });

  it("uses samAccountName as member secondary when different from primary", () => {
    const member = {
      displayName: "John Doe",
      name: "John Doe",
      samAccountName: "john.doe",
      distinguishedName: "CN=John Doe,OU=Users,DC=example,DC=com",
    };
    const primary = getAdGroupMemberPrimaryLabel(member);
    const secondary = getAdGroupMemberSecondaryLabel(member, primary);

    assert.equal(primary, "John Doe");
    assert.equal(secondary, "john.doe");
  });

  it("does not include description in member secondary label", () => {
    const member = {
      displayName: "VPN Users",
      name: "VPN Users",
      samAccountName: "VPN Users",
      description: "VPN access group",
      distinguishedName: "CN=VPN Users,OU=Groups,DC=example,DC=com",
    };
    const primary = getAdGroupMemberPrimaryLabel(member);

    assert.equal(getAdGroupMemberSecondaryLabel(member, primary), null);
  });
});
