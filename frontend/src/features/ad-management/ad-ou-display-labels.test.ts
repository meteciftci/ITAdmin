import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  formatAdOrganizationalUnitCount,
  getAdOrganizationalUnitAttributeName,
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

  it("uses ou as attribute name when available", () => {
    assert.equal(
      getAdOrganizationalUnitAttributeName({
        ou: "BT",
        name: "BT Name",
        distinguishedName: sampleDn,
      }),
      "BT",
    );
  });

  it("falls back to name when ou is missing", () => {
    assert.equal(
      getAdOrganizationalUnitAttributeName({
        ou: null,
        name: "BT Name",
        distinguishedName: sampleDn,
      }),
      "BT Name",
    );
  });

  it("falls back to parsed OU RDN when ou and name are missing", () => {
    assert.equal(
      getAdOrganizationalUnitAttributeName({
        ou: null,
        name: null,
        distinguishedName: sampleDn,
      }),
      "BT",
    );
  });

  it("returns null attribute name when no ou, name, or parseable RDN exists", () => {
    assert.equal(
      getAdOrganizationalUnitAttributeName({
        ou: null,
        name: null,
        distinguishedName: "DC=corp,DC=local",
      }),
      null,
    );
  });
});

describe("ad organizational unit list columns", () => {
  it("shows location in first column and ou attribute name in ouName column", () => {
    const columnsSource = readFileSync(
      new URL("./ad-ous-columns.tsx", import.meta.url),
      "utf8",
    );

    assert.match(columnsSource, /getAdOrganizationalUnitSecondaryLabel/);
    assert.match(columnsSource, /getAdOrganizationalUnitAttributeName/);
    assert.match(columnsSource, /id: "ouName"/);
    assert.match(columnsSource, /organizationalUnits\.table\.ouName/);
    assert.doesNotMatch(columnsSource, /id: "location"/);
    assert.doesNotMatch(columnsSource, /organizationalUnits\.table\.location/);
    assert.doesNotMatch(columnsSource, /getAdOrganizationalUnitParentPath/);
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
