import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildAccountStatusComparisonRows,
  buildCoreFieldComparisonRows,
  buildGenericSnapshotEntries,
  buildLockStatusComparisonRows,
  buildMappedAttributeComparisonRows,
  buildMembershipComparisonRows,
  buildOuMoveComparisonRows,
  formatSnapshotBoolean,
  getSnapshotRenderStrategy,
  parseAdOperationSnapshot,
  parseNestedAdOperationSnapshot,
  parseRequestSummaryEntries,
  resolveSnapshotUser,
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

const booleanLabels = { yes: "Yes", no: "No" };

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

describe("parseNestedAdOperationSnapshot", () => {
  it("parses nested account snapshots for UserEnable", () => {
    const before = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "UserEnable",
        user: {
          id: "81e3c58c-99bc-4454-9edd-cfe4abb894b4",
          samAccountName: "mete.test2",
          userPrincipalName: "mete.test1@mugla.bel.tr",
          distinguishedName: "CN=Mete TEST,DC=example,DC=com",
        },
        account: {
          isEnabled: false,
          isLocked: false,
          userAccountControl: 514,
        },
      }),
    );
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({
        account: {
          isEnabled: true,
          isLocked: false,
          userAccountControl: 512,
        },
        notifications: "Notifications: 0 queued, 0 skipped.",
      }),
    );

    const rows = buildAccountStatusComparisonRows(
      before,
      after,
      (value) => formatSnapshotBoolean(value, booleanLabels),
    );

    assert.equal(resolveSnapshotUser(before, after)?.samAccountName, "mete.test2");
    assert.equal(rows.find((row) => row.key === "isEnabled")?.before, "No");
    assert.equal(rows.find((row) => row.key === "isEnabled")?.after, "Yes");
    assert.equal(rows.find((row) => row.key === "isEnabled")?.changed, true);
    assert.equal(after?.notifications, "Notifications: 0 queued, 0 skipped.");
  });

  it("builds lock status rows for UserUnlock", () => {
    const before = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "UserUnlock",
        user: {
          id: "81e3c58c-99bc-4454-9edd-cfe4abb894b4",
          samAccountName: "mete.test2",
        },
        account: {
          isLocked: true,
          lockoutTime: "133000000000000",
          userAccountControl: 512,
        },
      }),
    );
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({
        account: {
          isLocked: false,
          lockoutTime: null,
          userAccountControl: 512,
        },
      }),
    );

    const rows = buildLockStatusComparisonRows(
      before,
      after,
      (value) => formatSnapshotBoolean(value, booleanLabels),
    );

    assert.equal(rows.find((row) => row.key === "isLocked")?.before, "Yes");
    assert.equal(rows.find((row) => row.key === "isLocked")?.after, "No");
    assert.equal(rows.find((row) => row.key === "lockoutTime")?.before, "133000000000000");
    assert.equal(rows.find((row) => row.key === "lockoutTime")?.after, null);
  });

  it("builds membership comparison for UserGroupAdd", () => {
    const before = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "UserGroupAdd",
        membership: { isDirectMember: false },
      }),
    );
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "UserGroupAdd",
        membership: { isDirectMember: true },
      }),
    );

    const rows = buildMembershipComparisonRows(
      before,
      after,
      (value) => formatSnapshotBoolean(value, booleanLabels),
    );

    assert.equal(rows[0]?.before, "No");
    assert.equal(rows[0]?.after, "Yes");
    assert.equal(rows[0]?.changed, true);
  });

  it("builds OU move comparison rows for UserOuMove snapshots", () => {
    const before = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "UserOuMove",
        user: {
          id: "550e8400-e29b-41d4-a716-446655440000",
          samAccountName: "mete.test2",
          userPrincipalName: "mete.test2@corp.local",
          distinguishedName: "CN=mete.test2,OU=Old,OU=Users,DC=corp,DC=local",
        },
        ou: { distinguishedName: "OU=Old,OU=Users,DC=corp,DC=local" },
      }),
    );
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "UserOuMove",
        user: {
          id: "550e8400-e29b-41d4-a716-446655440000",
          samAccountName: "mete.test2",
          userPrincipalName: "mete.test2@corp.local",
          distinguishedName: "CN=mete.test2,OU=New,OU=Users,DC=corp,DC=local",
        },
        ou: { distinguishedName: "OU=New,OU=Users,DC=corp,DC=local" },
      }),
    );

    const rows = buildOuMoveComparisonRows(before, after);
    const ouRow = rows.find((row) => row.key === "ou");
    const dnRow = rows.find((row) => row.key === "distinguishedName");

    assert.equal(ouRow?.before, "OU=Old,OU=Users,DC=corp,DC=local");
    assert.equal(ouRow?.after, "OU=New,OU=Users,DC=corp,DC=local");
    assert.equal(ouRow?.changed, true);
    assert.equal(dnRow?.before, "CN=mete.test2,OU=Old,OU=Users,DC=corp,DC=local");
    assert.equal(dnRow?.after, "CN=mete.test2,OU=New,OU=Users,DC=corp,DC=local");
    assert.equal(dnRow?.monoBefore, true);
    assert.equal(dnRow?.monoAfter, true);
  });

  it("parses ou node from UserOuMove snapshot", () => {
    const snapshot = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "UserOuMove",
        ou: { DistinguishedName: "OU=Target,DC=corp,DC=local" },
      }),
    );

    assert.equal(snapshot?.ou?.distinguishedName, "OU=Target,DC=corp,DC=local");
  });

  it("builds membership comparison for UserGroupRemove", () => {
    const before = parseNestedAdOperationSnapshot(
      JSON.stringify({ membership: { isDirectMember: true } }),
    );
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({ membership: { isDirectMember: false } }),
    );

    const rows = buildMembershipComparisonRows(
      before,
      after,
      (value) => formatSnapshotBoolean(value, booleanLabels),
    );

    assert.equal(rows[0]?.before, "Yes");
    assert.equal(rows[0]?.after, "No");
  });

  it("parses created user snapshot for UserCreate", () => {
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "CreateUser",
        user: {
          id: "81e3c58c-99bc-4454-9edd-cfe4abb894b4",
          samAccountName: "mete.test",
          userPrincipalName: "mete.test@mugla.bel.tr",
          displayName: "Mete Test",
          distinguishedName: "CN=Mete Test,DC=example,DC=com",
        },
        account: { isEnabled: true },
        mappedAttributes: [{ logicalField: "gender", values: ["••••"] }],
      }),
    );

    assert.equal(after?.user?.displayName, "Mete Test");
    assert.equal(after?.account?.isEnabled, true);
    assert.equal(after?.mappedAttributes[0]?.displayValue, "••••");
    assert.equal(JSON.stringify(after).includes("initialPassword"), false);
  });

  it("formats boolean values as readable yes/no labels", () => {
    assert.equal(formatSnapshotBoolean(true, booleanLabels), "Yes");
    assert.equal(formatSnapshotBoolean(false, booleanLabels), "No");
    assert.equal(formatSnapshotBoolean(null, booleanLabels), null);
  });

  it("returns generic fallback entries for unknown snapshot shapes", () => {
    const entries = buildGenericSnapshotEntries({
      customField: "value",
      nested: { inner: "data" },
    });

    assert.ok(entries.some((entry) => entry.key === "customField"));
    assert.ok(entries.some((entry) => entry.key === "nested" && entry.nested?.length === 1));
  });
});

describe("getSnapshotRenderStrategy", () => {
  it("maps known operation types to dedicated strategies", () => {
    assert.equal(getSnapshotRenderStrategy("UserUpdate"), "userUpdate");
    assert.equal(getSnapshotRenderStrategy("CreateUser"), "userCreate");
    assert.equal(getSnapshotRenderStrategy("UserEnable"), "accountStatus");
    assert.equal(getSnapshotRenderStrategy("UserDisable"), "accountStatus");
    assert.equal(getSnapshotRenderStrategy("UserUnlock"), "lockStatus");
    assert.equal(getSnapshotRenderStrategy("UserGroupAdd"), "groupMembership");
    assert.equal(getSnapshotRenderStrategy("UserGroupRemove"), "groupMembership");
    assert.equal(getSnapshotRenderStrategy("UserOuMove"), "ouMove");
  });

  it("falls back to generic for unknown operation types", () => {
    assert.equal(getSnapshotRenderStrategy("SettingsUpdated"), "generic");
    assert.notEqual(getSnapshotRenderStrategy("UserOuMove"), "generic");
  });

  it("keeps UserGroupAdd on groupMembership strategy", () => {
    assert.equal(getSnapshotRenderStrategy("UserGroupAdd"), "groupMembership");
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

  it("does not expose initialPassword in request summary rendering data", () => {
    const entries = parseRequestSummaryEntries(
      JSON.stringify({
        operation: "CreateUser",
        samAccountName: "mete.test",
        mappedAttributeFields: ["gender"],
      }),
    );

    assert.ok(entries);
    assert.equal(entries?.some((entry) => entry.key === "initialPassword"), false);
  });
});
