import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  buildAccountStatusComparisonRows,
  buildCoreFieldComparisonRows,
  buildGenericSnapshotEntries,
  buildGroupComparisonRows,
  buildLockStatusComparisonRows,
  buildMappedAttributeComparisonRows,
  buildMembershipComparisonRows,
  buildOuMoveComparisonRows,
  formatSnapshotBoolean,
  getSnapshotRenderStrategy,
  hasNestedSnapshotContent,
  readSnapshotDeletedFlag,
  resolveSnapshotComputer,
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

  it("builds ou move comparison rows for GroupMoveOu snapshots", () => {
    const before = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "GroupMoveOu",
        group: {
          distinguishedName: "CN=VPN Users,OU=Source,OU=Groups,DC=corp,DC=local",
        },
        ou: { distinguishedName: "OU=Source,OU=Groups,DC=corp,DC=local" },
      }),
    );
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "GroupMoveOu",
        group: {
          distinguishedName: "CN=VPN Users,OU=Target,OU=Groups,DC=corp,DC=local",
        },
        ou: { distinguishedName: "OU=Target,OU=Groups,DC=corp,DC=local" },
      }),
    );

    const rows = buildOuMoveComparisonRows(before, after);
    const ouRow = rows.find((row) => row.key === "ou");
    const dnRow = rows.find((row) => row.key === "distinguishedName");

    assert.equal(ouRow?.before, "OU=Source,OU=Groups,DC=corp,DC=local");
    assert.equal(ouRow?.after, "OU=Target,OU=Groups,DC=corp,DC=local");
    assert.equal(dnRow?.before, "CN=VPN Users,OU=Source,OU=Groups,DC=corp,DC=local");
    assert.equal(dnRow?.after, "CN=VPN Users,OU=Target,OU=Groups,DC=corp,DC=local");
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

describe("parseSnapshotAccountExpiration", () => {
  it("prefers accountExpiresDate and falls back to legacy accountExpiresAt", () => {
    const withDateOnly = parseNestedAdOperationSnapshot(
      JSON.stringify({
        accountExpiration: {
          neverExpires: false,
          accountExpiresDate: "2026-06-27",
        },
      }),
    );

    assert.equal(withDateOnly?.accountExpiration?.accountExpiresDate, "2026-06-27");

    const withLegacy = parseNestedAdOperationSnapshot(
      JSON.stringify({
        accountExpiration: {
          neverExpires: false,
          accountExpiresAt: "2026-06-27T00:00:00Z",
        },
      }),
    );

    assert.equal(withLegacy?.accountExpiration?.accountExpiresDate, "2026-06-27T00:00:00Z");
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
    assert.equal(getSnapshotRenderStrategy("GroupMoveOu"), "ouMove");
    assert.equal(getSnapshotRenderStrategy("UserManagerUpdate"), "userManagerUpdate");
    assert.equal(getSnapshotRenderStrategy("UserAccountExpirationUpdate"), "userAccountExpirationUpdate");
    assert.equal(getSnapshotRenderStrategy("GroupCreate"), "groupCreate");
    assert.equal(getSnapshotRenderStrategy("GroupUpdate"), "groupUpdate");
    assert.equal(getSnapshotRenderStrategy("GroupDelete"), "groupDelete");
    assert.equal(getSnapshotRenderStrategy("ComputerDelete"), "computerDelete");
    assert.equal(getSnapshotRenderStrategy("ComputerEnable"), "accountStatus");
    assert.equal(getSnapshotRenderStrategy("ComputerDisable"), "accountStatus");
    assert.equal(getSnapshotRenderStrategy("ComputerMoveOu"), "ouMove");
    assert.equal(getSnapshotRenderStrategy("GroupMemberAdd"), "groupMember");
    assert.equal(getSnapshotRenderStrategy("GroupMemberRemove"), "groupMember");
  });

  it("falls back to generic for unknown operation types", () => {
    assert.equal(getSnapshotRenderStrategy("SettingsUpdated"), "generic");
    assert.notEqual(getSnapshotRenderStrategy("UserOuMove"), "generic");
    assert.notEqual(getSnapshotRenderStrategy("UserManagerUpdate"), "generic");
    assert.notEqual(getSnapshotRenderStrategy("UserAccountExpirationUpdate"), "generic");
  });

  it("keeps UserGroupAdd on groupMembership strategy", () => {
    assert.equal(getSnapshotRenderStrategy("UserGroupAdd"), "groupMembership");
  });

  it("keeps GroupMemberAdd on groupMember strategy", () => {
    assert.equal(getSnapshotRenderStrategy("GroupMemberAdd"), "groupMember");
    assert.equal(getSnapshotRenderStrategy("GroupMemberRemove"), "groupMember");
    assert.notEqual(getSnapshotRenderStrategy("GroupMemberAdd"), "generic");
    assert.notEqual(getSnapshotRenderStrategy("ComputerDelete"), "generic");
  });
});

describe("parseNestedAdOperationSnapshot ComputerDelete snapshots", () => {
  const beforeSnapshot = {
    operation: "ComputerDelete",
    computer: {
      id: "11111111-2222-3333-4444-555555555555",
      samAccountName: "PC-01$",
      name: "PC-01",
      distinguishedName: "CN=PC-01,OU=Computers,DC=corp,DC=local",
    },
    account: {
      isEnabled: true,
      userAccountControl: 4098,
      primaryGroupId: 515,
    },
  };

  const afterSnapshot = {
    operation: "ComputerDelete",
    deleted: true,
    computer: {
      id: "11111111-2222-3333-4444-555555555555",
      samAccountName: "PC-01$",
      name: "PC-01",
      distinguishedName: "CN=PC-01,OU=Computers,DC=corp,DC=local",
    },
  };

  it("parses ComputerDelete computer and account fields in before snapshot", () => {
    const parsed = parseNestedAdOperationSnapshot(JSON.stringify(beforeSnapshot));

    assert.equal(parsed?.operation, "ComputerDelete");
    assert.equal(parsed?.computer?.id, "11111111-2222-3333-4444-555555555555");
    assert.equal(parsed?.computer?.samAccountName, "PC-01$");
    assert.equal(parsed?.computer?.name, "PC-01");
    assert.equal(
      parsed?.computer?.distinguishedName,
      "CN=PC-01,OU=Computers,DC=corp,DC=local",
    );
    assert.equal(parsed?.account?.isEnabled, true);
    assert.equal(parsed?.account?.userAccountControl, 4098);
    assert.equal(parsed?.account?.primaryGroupId, 515);
    assert.equal(parsed?.user?.samAccountName, "PC-01$");
  });

  it("preserves deleted flag on after snapshot raw record", () => {
    const parsed = parseNestedAdOperationSnapshot(JSON.stringify(afterSnapshot));

    assert.equal(readSnapshotDeletedFlag(parsed), true);
    assert.equal(parsed?.rawRecord.deleted, true);
  });

  it("resolves deleted computer from before and after snapshots", () => {
    const before = parseNestedAdOperationSnapshot(JSON.stringify(beforeSnapshot));
    const after = parseNestedAdOperationSnapshot(JSON.stringify(afterSnapshot));

    assert.equal(resolveSnapshotComputer(before, after)?.name, "PC-01");
    assert.equal(resolveSnapshotComputer(before, null)?.samAccountName, "PC-01$");
  });

  it("reports nested snapshot content when computer field exists", () => {
    const parsed = parseNestedAdOperationSnapshot(JSON.stringify(beforeSnapshot));

    assert.equal(hasNestedSnapshotContent(parsed), true);
  });
});

describe("parseNestedAdOperationSnapshot group member snapshots", () => {
  it("parses member snapshot fields", () => {
    const parsed = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "GroupMemberAdd",
        group: {
          id: "550e8400-e29b-41d4-a716-446655440000",
          displayName: "VPN Users",
          name: "vpn-users",
          samAccountName: "vpn-users",
          distinguishedName: "CN=VPN Users,DC=example,DC=com",
        },
        member: {
          id: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
          type: "User",
          displayName: "Mete Çiftçi",
          samAccountName: "mete.ciftci",
          userPrincipalName: "mete.ciftci@mugla.bel.tr",
          distinguishedName: "CN=Mete,DC=example,DC=com",
        },
        membership: { isDirectMember: false },
      }),
    );

    assert.equal(parsed?.member?.samAccountName, "mete.ciftci");
    assert.equal(parsed?.member?.type, "User");
    assert.equal(parsed?.membership?.isDirectMember, false);
  });

  it("builds membership comparison for GroupMemberAdd", () => {
    const before = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "GroupMemberAdd",
        membership: { isDirectMember: false },
      }),
    );
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "GroupMemberAdd",
        membership: { isDirectMember: true },
      }),
    );

    const rows = buildMembershipComparisonRows(before, after, (value) =>
      value === null || value === undefined ? null : value ? "yes" : "no",
    );

    assert.equal(rows[0]?.before, "no");
    assert.equal(rows[0]?.after, "yes");
    assert.equal(rows[0]?.changed, true);
  });
});

describe("parseNestedAdOperationSnapshot group snapshots", () => {
  const nestedGroupSnapshot = {
    operation: "GroupUpdate",
    group: {
      id: "550e8400-e29b-41d4-a716-446655440000",
      displayName: "VPN Users",
      name: "vpn-users",
      cn: "VPN Users",
      samAccountName: "vpn-users",
      description: "VPN access group",
      distinguishedName: "CN=VPN Users,OU=Groups,DC=corp,DC=local",
      groupScope: "Global",
      securityEnabled: true,
      groupType: -2147483646,
    },
  };

  it("parses nested group fields", () => {
    const parsed = parseNestedAdOperationSnapshot(JSON.stringify(nestedGroupSnapshot));

    assert.ok(parsed?.group);
    assert.equal(parsed?.group?.id, "550e8400-e29b-41d4-a716-446655440000");
    assert.equal(parsed?.group?.displayName, "VPN Users");
    assert.equal(parsed?.group?.name, "vpn-users");
    assert.equal(parsed?.group?.cn, "VPN Users");
    assert.equal(parsed?.group?.samAccountName, "vpn-users");
    assert.equal(parsed?.group?.description, "VPN access group");
    assert.equal(parsed?.group?.distinguishedName, "CN=VPN Users,OU=Groups,DC=corp,DC=local");
    assert.equal(parsed?.group?.groupScope, "Global");
    assert.equal(parsed?.group?.securityEnabled, true);
    assert.equal(parsed?.group?.groupType, -2147483646);
  });

  it("parses GroupDelete memberCount and memberOfCount", () => {
    const parsed = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "GroupDelete",
        group: {
          id: "550e8400-e29b-41d4-a716-446655440000",
          displayName: "VPN Users",
          name: "vpn-users",
          samAccountName: "vpn-users",
          memberCount: 12,
          memberOfCount: 3,
        },
      }),
    );

    assert.equal(parsed?.group?.memberCount, 12);
    assert.equal(parsed?.group?.memberOfCount, 3);
  });

  it("builds group comparison rows for rename scenario", () => {
    const before = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "GroupUpdate",
        group: {
          name: "vpn-users-old",
          cn: "VPN Users Old",
          distinguishedName: "CN=VPN Users Old,OU=Groups,DC=corp,DC=local",
          samAccountName: "vpn-users-old",
        },
      }),
    );
    const after = parseNestedAdOperationSnapshot(
      JSON.stringify({
        operation: "GroupUpdate",
        group: {
          name: "vpn-users",
          cn: "VPN Users",
          distinguishedName: "CN=VPN Users,OU=Groups,DC=corp,DC=local",
          samAccountName: "vpn-users",
        },
      }),
    );

    const rows = buildGroupComparisonRows(
      before,
      after,
      (value) => formatSnapshotBoolean(value, booleanLabels),
    );

    const nameRow = rows.find((row) => row.key === "name");
    const cnRow = rows.find((row) => row.key === "cn");
    const dnRow = rows.find((row) => row.key === "distinguishedName");

    assert.equal(nameRow?.changed, true);
    assert.equal(cnRow?.changed, true);
    assert.equal(dnRow?.changed, true);
    assert.equal(dnRow?.monoBefore, true);
    assert.equal(dnRow?.monoAfter, true);
  });
});

describe("AdOperationLogSnapshotDetail group render wiring", () => {
  it("uses dedicated group create/update sections instead of generic fallback", () => {
    const detailSource = readFileSync(
      new URL("./components/AdOperationLogSnapshotDetail.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /GroupCreateSnapshotSections/);
    assert.match(detailSource, /GroupUpdateSnapshotSections/);
    assert.match(detailSource, /GroupDeleteSnapshotSections/);
    assert.match(detailSource, /case "groupCreate"/);
    assert.match(detailSource, /case "groupUpdate"/);
    assert.match(detailSource, /case "groupDelete"/);
    assert.match(detailSource, /snapshotSections\.deletedGroup/);
    assert.match(detailSource, /buildGroupComparisonRows/);
    assert.match(detailSource, /ComparisonTable/);
    assert.match(detailSource, /getGroupFieldEntries/);
  });
});

describe("AdOperationLogSnapshotDetail ComputerDelete render wiring", () => {
  it("uses dedicated ComputerDelete sections instead of generic fallback", () => {
    const detailSource = readFileSync(
      new URL("./components/AdOperationLogSnapshotDetail.tsx", import.meta.url),
      "utf8",
    );

    assert.match(detailSource, /ComputerDeleteSnapshotSections/);
    assert.match(detailSource, /case "computerDelete"/);
    assert.match(detailSource, /snapshotSections\.deletedComputer/);
    assert.match(detailSource, /snapshotSections\.deleteResult/);
    assert.match(detailSource, /getComputerFieldEntries/);
    assert.match(detailSource, /getComputerDeleteAccountFieldEntries/);
    assert.match(detailSource, /KeyValueGrid/);
    assert.match(detailSource, /resolveSnapshotComputer/);
    assert.match(detailSource, /readSnapshotDeletedFlag/);
    assert.doesNotMatch(
      detailSource.slice(
        detailSource.indexOf("function ComputerDeleteSnapshotSections"),
        detailSource.indexOf("function GroupDeleteSnapshotSections"),
      ),
      /GenericSnapshotSections/,
    );
  });
});

describe("adOperationLogs group snapshot locale labels", () => {
  it("includes TR group log labels", () => {
    const trLocale = readFileSync(
      new URL("../../locales/tr/adOperationLogs.json", import.meta.url),
      "utf8",
    );

    assert.match(trLocale, /"createdGroup": "Oluşturulan Grup"/);
    assert.match(trLocale, /"groupUpdate": "Grup Bilgileri"/);
    assert.match(trLocale, /"deletedGroup": "Silinen Grup"/);
    assert.match(trLocale, /"GroupDelete": "Grup silme"/);
    assert.match(trLocale, /"memberCount": "Üye Sayısı"/);
    assert.match(trLocale, /"memberOfCount": "Üye Olduğu Grup Sayısı"/);
    assert.match(trLocale, /"groupDisplayName": "Grup Görünen Ad"/);
    assert.match(trLocale, /"groupName": "Grup Adı"/);
  });

  it("includes TR ComputerDelete snapshot labels", () => {
    const trLocale = readFileSync(
      new URL("../../locales/tr/adOperationLogs.json", import.meta.url),
      "utf8",
    );
    const enLocale = readFileSync(
      new URL("../../locales/en/adOperationLogs.json", import.meta.url),
      "utf8",
    );

    assert.match(trLocale, /"deletedComputer": "Silinen bilgisayar"/);
    assert.match(trLocale, /"deleteResult": "Silme sonucu"/);
    assert.match(trLocale, /"computerId": "Bilgisayar ID"/);
    assert.match(trLocale, /"primaryGroupId": "primaryGroupID"/);
    assert.match(trLocale, /"deleted": "Silindi"/);
    assert.match(trLocale, /"ComputerDelete": "Bilgisayar silme"/);
    assert.match(enLocale, /"deletedComputer": "Deleted computer"/);
    assert.match(enLocale, /"deleteResult": "Delete result"/);
    assert.match(enLocale, /"ComputerDelete": "Computer delete"/);
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
