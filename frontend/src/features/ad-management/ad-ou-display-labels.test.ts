import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  formatAdOrganizationalUnitCount,
  getAdOrganizationalUnitParentPath,
  getAdOrganizationalUnitPrimaryLabel,
  getAdOrganizationalUnitSecondaryLabel,
  resolveOrganizationalUnitRenameName,
} from "./ad-ou-display-labels.ts";

const sampleDn = "OU=BT,OU=Departments,DC=corp,DC=local";
const emptyText = "N/A";

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

  it("formats null counts with provided empty text", () => {
    assert.equal(formatAdOrganizationalUnitCount(null, emptyText), emptyText);
    assert.equal(formatAdOrganizationalUnitCount(undefined, emptyText), emptyText);
    assert.equal(formatAdOrganizationalUnitCount(3, emptyText), "3");
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

  it("resolves rename name from ou before name", () => {
    assert.equal(
      resolveOrganizationalUnitRenameName({
        ou: "BT",
        name: "Legacy",
        distinguishedName: sampleDn,
      }),
      "BT",
    );
  });
});

describe("ad organizational unit technical field", () => {
  it("uses wrapping classes for long technical values", () => {
    const source = readFileSync(
      new URL("./components/AdOrganizationalUnitTechnicalField.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /break-words/);
    assert.match(source, /break-all/);
    assert.match(source, /overflow-wrap:anywhere/);
    assert.match(source, /whitespace-pre-wrap/);
    assert.match(source, /common:notAvailable/);
    assert.match(source, /min-w-0/);
  });
});

describe("ad organizational unit count badge", () => {
  it("uses i18n empty text and supports badge and card variants", () => {
    const source = readFileSync(
      new URL("./components/AdOrganizationalUnitCountBadge.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /formatAdOrganizationalUnitCount/);
    assert.match(source, /common:notAvailable/);
    assert.match(source, /variant = "badge"/);
    assert.match(source, /variant === "card"/);
    assert.match(source, /aria-label/);
  });
});
