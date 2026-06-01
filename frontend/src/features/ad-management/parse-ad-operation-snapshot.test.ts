import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildCoreFieldComparisonRows,
  buildMappedAttributeComparisonRows,
  parseAdOperationSnapshot,
  parseRequestSummaryEntries,
} from "./parse-ad-operation-snapshot.ts";

const sampleSnapshot = {
  givenName: "Ali",
  surname: "Veli",
  displayName: "Ali Veli",
  samAccountName: "ali.veli",
  mappedAttributes: [
    { logicalField: "gender", values: ["Erkek"] },
    { logicalField: "employeeId", values: ["100"] },
  ],
};

describe("parseAdOperationSnapshot", () => {
  it("parses normal JSON snapshot strings", () => {
    const parsed = parseAdOperationSnapshot(JSON.stringify(sampleSnapshot));
    assert.ok(parsed);
    assert.equal(parsed?.core.givenName, "Ali");
    assert.equal(parsed?.mappedAttributes.length, 2);
  });

  it("parses double-encoded JSON snapshot strings", () => {
    const parsed = parseAdOperationSnapshot(JSON.stringify(JSON.stringify(sampleSnapshot)));
    assert.equal(parsed?.core.surname, "Veli");
  });

  it("builds core comparison rows with changed highlight", () => {
    const before = parseAdOperationSnapshot(
      JSON.stringify({ ...sampleSnapshot, department: "IT" }),
    );
    const after = parseAdOperationSnapshot(
      JSON.stringify({ ...sampleSnapshot, department: "HR" }),
    );

    const rows = buildCoreFieldComparisonRows(before, after);
    const departmentRow = rows.find((row) => row.key === "department");

    assert.ok(departmentRow);
    assert.equal(departmentRow?.before, "IT");
    assert.equal(departmentRow?.after, "HR");
    assert.equal(departmentRow?.changed, true);
  });

  it("compares mapped attributes by logicalField", () => {
    const before = parseAdOperationSnapshot(
      JSON.stringify({
        mappedAttributes: [{ logicalField: "gender", values: ["Erkek"] }],
      }),
    );
    const after = parseAdOperationSnapshot(
      JSON.stringify({
        mappedAttributes: [{ logicalField: "gender", values: ["Kadın"] }],
      }),
    );

    const rows = buildMappedAttributeComparisonRows(before, after);
    assert.equal(rows.length, 1);
    assert.equal(rows[0]?.key, "gender");
    assert.equal(rows[0]?.changed, true);
  });

  it("formats mapped attribute values as comma-separated text", () => {
    const parsed = parseAdOperationSnapshot(
      JSON.stringify({
        mappedAttributes: [{ logicalField: "groups", values: ["A", "B"] }],
      }),
    );

    assert.equal(parsed?.mappedAttributes[0]?.displayValue, "A, B");
  });
});

describe("parseRequestSummaryEntries", () => {
  it("returns key/value entries for request summary objects", () => {
    const entries = parseRequestSummaryEntries(
      JSON.stringify({ changeStatus: "NoChangesDetected", requestedPage: 1 }),
    );

    assert.ok(entries);
    assert.equal(entries?.find((entry) => entry.key === "changeStatus")?.displayValue, "NoChangesDetected");
  });
});
