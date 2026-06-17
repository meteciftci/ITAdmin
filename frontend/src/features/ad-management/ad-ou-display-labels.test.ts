import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  formatAdOrganizationalUnitCount,
  getAdOrganizationalUnitParentPath,
  getAdOrganizationalUnitPrimaryLabel,
  getAdOrganizationalUnitSecondaryLabel,
} from "./ad-ou-display-labels.ts";

const sampleDn = "OU=BT,OU=Departments,DC=corp,DC=local";

describe("ad-ou-display-labels", () => {
  it("uses displayLabel as primary label when available", () => {
    const label = getAdOrganizationalUnitPrimaryLabel({
      displayLabel: "BT Department",
      displayName: "BT Display",
      ou: "BT",
      name: "BT Name",
      distinguishedName: sampleDn,
    });

    assert.equal(label, "BT Department");
  });

  it("prefers displayName over ou and name", () => {
    const label = getAdOrganizationalUnitPrimaryLabel({
      displayName: "BT Display",
      ou: "BT",
      name: "BT Name",
      distinguishedName: sampleDn,
    });

    assert.equal(label, "BT Display");
  });

  it("prefers ou over name when displayName is missing", () => {
    const label = getAdOrganizationalUnitPrimaryLabel({
      displayName: null,
      ou: "BT",
      name: "BT Name",
      distinguishedName: sampleDn,
    });

    assert.equal(label, "BT");
  });

  it("falls back to parsed RDN label before distinguishedName", () => {
    const label = getAdOrganizationalUnitPrimaryLabel({
      displayName: null,
      ou: null,
      name: null,
      distinguishedName: sampleDn,
    });

    assert.equal(label, "BT");
  });

  it("formats null counts as dash", () => {
    assert.equal(formatAdOrganizationalUnitCount(null), "-");
    assert.equal(formatAdOrganizationalUnitCount(undefined), "-");
    assert.equal(formatAdOrganizationalUnitCount(3), "3");
  });

  it("derives parent path from canonical name", () => {
    assert.equal(
      getAdOrganizationalUnitParentPath("corp.local/Departments/BT"),
      "corp.local/Departments",
    );
    assert.equal(getAdOrganizationalUnitParentPath("corp.local"), null);
  });

  it("returns secondary label from canonical name when different from primary", () => {
    const item = {
      displayLabel: "BT",
      distinguishedName: sampleDn,
      canonicalName: "corp.local/Departments/BT",
    };
    const primary = getAdOrganizationalUnitPrimaryLabel(item);
    const secondary = getAdOrganizationalUnitSecondaryLabel(item, primary);

    assert.equal(primary, "BT");
    assert.equal(secondary, "corp.local/Departments/BT");
  });
});
