import type { SnapshotRenderStrategy } from "./snapshot/snapshot-types.ts";

export type {
  ParsedAdOperationSnapshot,
  ParsedMappedSnapshotAttribute,
  ParsedNestedAdOperationSnapshot,
  ParsedSnapshotAccount,
  ParsedSnapshotAccountExpiration,
  ParsedSnapshotComputer,
  ParsedSnapshotDeletedObject,
  ParsedSnapshotGroup,
  ParsedSnapshotManager,
  ParsedSnapshotMember,
  ParsedSnapshotMembership,
  ParsedSnapshotOrganizationalUnit,
  ParsedSnapshotOu,
  ParsedSnapshotRestoredObject,
  ParsedSnapshotUser,
  GenericSnapshotEntry,
  SnapshotComparisonRow,
  SnapshotComputerComparisonFieldKey,
  SnapshotCoreFieldKey,
  SnapshotGroupComparisonFieldKey,
  SnapshotRenderStrategy,
} from "./snapshot/snapshot-types.ts";
export {
  SNAPSHOT_COMPUTER_COMPARISON_FIELD_KEYS,
  SNAPSHOT_CORE_FIELD_KEYS,
  SNAPSHOT_GROUP_COMPARISON_FIELD_KEYS,
} from "./snapshot/snapshot-types.ts";

export { formatSnapshotBoolean, formatSnapshotValue } from "./snapshot/snapshot-primitives.ts";

export {
  buildCoreFieldComparisonRows,
  buildMappedAttributeComparisonRows,
  hasSnapshotContent,
  parseAdOperationSnapshot,
  parseRequestSummaryEntries,
} from "./snapshot/snapshot-flat.ts";

export { parseNestedAdOperationSnapshot } from "./snapshot/snapshot-nested.ts";

export {
  buildAccountExpirationComparisonRows,
  buildAccountStatusComparisonRows,
  buildComputerComparisonRows,
  buildGenericSnapshotEntries,
  buildGenericSnapshotSections,
  buildGroupComparisonRows,
  buildLockStatusComparisonRows,
  buildManagerComparisonRows,
  buildMembershipComparisonRows,
  buildOrganizationalUnitComparisonRows,
  buildOuMoveComparisonRows,
  hasNestedSnapshotContent,
  readSnapshotDeletedFlag,
  readSnapshotRootDescription,
  resolveSnapshotComputer,
  resolveSnapshotGroup,
  resolveSnapshotMember,
  resolveSnapshotOu,
  resolveSnapshotUser,
} from "./snapshot/snapshot-comparisons.ts";

const ACCOUNT_STATUS_OPERATION_TYPES = new Set([
  "UserEnable",
  "UserDisable",
  "ComputerEnable",
  "ComputerDisable",
]);
const LOCK_STATUS_OPERATION_TYPES = new Set(["UserUnlock"]);
const GROUP_MEMBERSHIP_OPERATION_TYPES = new Set([
  "UserGroupAdd",
  "UserGroupRemove",
  "ComputerGroupAdd",
  "ComputerGroupRemove",
]);
const GROUP_MEMBER_OPERATION_TYPES = new Set(["GroupMemberAdd", "GroupMemberRemove"]);
const OU_MOVE_OPERATION_TYPES = new Set(["UserOuMove", "GroupMoveOu", "ComputerMoveOu"]);
const ORGANIZATIONAL_UNIT_OPERATION_TYPES = new Set([
  "OrganizationalUnitCreate",
  "OrganizationalUnitRename",
  "OrganizationalUnitMove",
  "OrganizationalUnitDelete",
]);
const USER_MANAGER_UPDATE_OPERATION_TYPES = new Set(["UserManagerUpdate"]);
const USER_ACCOUNT_EXPIRATION_UPDATE_OPERATION_TYPES = new Set(["UserAccountExpirationUpdate"]);

export function getSnapshotRenderStrategy(operationType: string): SnapshotRenderStrategy {
  if (operationType === "UserUpdate") {
    return "userUpdate";
  }
  if (operationType === "CreateUser") {
    return "userCreate";
  }
  if (operationType === "GroupCreate") {
    return "groupCreate";
  }
  if (operationType === "GroupUpdate") {
    return "groupUpdate";
  }
  if (operationType === "GroupDelete") {
    return "groupDelete";
  }
  if (operationType === "ComputerDelete") {
    return "computerDelete";
  }
  if (operationType === "ComputerUpdate") {
    return "computerUpdate";
  }
  if (operationType === "DeletedObjectRestore") {
    return "deletedObjectRestore";
  }
  if (ACCOUNT_STATUS_OPERATION_TYPES.has(operationType)) {
    return "accountStatus";
  }
  if (LOCK_STATUS_OPERATION_TYPES.has(operationType)) {
    return "lockStatus";
  }
  if (GROUP_MEMBERSHIP_OPERATION_TYPES.has(operationType)) {
    return "groupMembership";
  }
  if (GROUP_MEMBER_OPERATION_TYPES.has(operationType)) {
    return "groupMember";
  }
  if (OU_MOVE_OPERATION_TYPES.has(operationType)) {
    return "ouMove";
  }
  if (ORGANIZATIONAL_UNIT_OPERATION_TYPES.has(operationType)) {
    return "organizationalUnit";
  }
  if (USER_MANAGER_UPDATE_OPERATION_TYPES.has(operationType)) {
    return "userManagerUpdate";
  }
  if (USER_ACCOUNT_EXPIRATION_UPDATE_OPERATION_TYPES.has(operationType)) {
    return "userAccountExpirationUpdate";
  }
  return "generic";
}
