import { getSnapshotRenderStrategy, type SnapshotRenderStrategy } from "./parse-ad-operation-snapshot.ts";

export type AdOperationLogCoverageRow = {
  operationType: string;
  backendConstantExists: boolean;
  frontendLabelExists: boolean;
  frontendDetailRendererExists: boolean;
  snapshotRendererExists: boolean;
  trLocaleExists: boolean;
  enLocaleExists: boolean;
  notes: string;
};

const DEDICATED_SNAPSHOT_STRATEGIES = new Set<SnapshotRenderStrategy>([
  "userUpdate",
  "userCreate",
  "groupCreate",
  "groupUpdate",
  "groupDelete",
  "computerDelete",
  "computerUpdate",
  "deletedObjectRestore",
  "accountStatus",
  "lockStatus",
  "groupMembership",
  "groupMember",
  "ouMove",
  "userManagerUpdate",
  "userAccountExpirationUpdate",
]);

/** Mirrors backend AdManagementOperationTypes public constants (inventory baseline). */
export const AD_OPERATION_LOG_COVERAGE_OPERATION_TYPES = [
  "SettingsUpdated",
  "SettingsValidated",
  "AttributeMappingCreated",
  "AttributeMappingUpdated",
  "AttributeMappingDeleted",
  "CreateUser",
  "UserUpdate",
  "UserEnable",
  "UserDisable",
  "UserUnlock",
  "UserGroupAdd",
  "UserGroupRemove",
  "UserOuMove",
  "UserManagerUpdate",
  "UserAccountExpirationUpdate",
  "GroupCreate",
  "GroupUpdate",
  "GroupDelete",
  "GroupMemberAdd",
  "GroupMemberRemove",
  "GroupMoveOu",
  "ComputerEnable",
  "ComputerDisable",
  "ComputerUpdate",
  "ComputerMoveOu",
  "ComputerDelete",
  "ComputerGroupAdd",
  "ComputerGroupRemove",
  "DeletedObjectRestore",
] as const;

export type AdOperationLogCoverageOperationType =
  (typeof AD_OPERATION_LOG_COVERAGE_OPERATION_TYPES)[number];

const COVERAGE_NOTES: Partial<Record<AdOperationLogCoverageOperationType, string>> = {
  SettingsUpdated: "Generic snapshot renderer; settings before/after snapshots.",
  SettingsValidated: "Generic snapshot renderer; validation summary only.",
  AttributeMappingCreated: "Generic snapshot renderer.",
  AttributeMappingUpdated: "Generic snapshot renderer.",
  AttributeMappingDeleted: "Generic snapshot renderer; before snapshot only on success.",
  ComputerEnable: "Uses accountStatus strategy (user-oriented sections for computer account).",
  ComputerDisable: "Uses accountStatus strategy (user-oriented sections for computer account).",
  ComputerUpdate: "Dedicated computerUpdate snapshot comparison renderer.",
  ComputerMoveOu: "ouMove strategy with computer-aware entity field grid.",
};

function hasDedicatedSnapshotRenderer(operationType: string): boolean {
  const strategy = getSnapshotRenderStrategy(operationType);
  return strategy !== "generic" && DEDICATED_SNAPSHOT_STRATEGIES.has(strategy);
}

export function buildAdOperationLogCoverageMatrix(
  trOperations: Record<string, string>,
  enOperations: Record<string, string>,
): AdOperationLogCoverageRow[] {
  return AD_OPERATION_LOG_COVERAGE_OPERATION_TYPES.map((operationType) => {
    const trLabel = trOperations[operationType];
    const enLabel = enOperations[operationType];
    const trLocaleExists = typeof trLabel === "string" && trLabel.trim().length > 0;
    const enLocaleExists = typeof enLabel === "string" && enLabel.trim().length > 0;
    const snapshotRendererExists = hasDedicatedSnapshotRenderer(operationType);

    return {
      operationType,
      backendConstantExists: true,
      frontendLabelExists: trLocaleExists && enLocaleExists,
      frontendDetailRendererExists: true,
      snapshotRendererExists,
      trLocaleExists,
      enLocaleExists,
      notes:
        COVERAGE_NOTES[operationType] ??
        (snapshotRendererExists ? "Dedicated snapshot renderer." : "Generic snapshot fallback."),
    };
  });
}

export function summarizeAdOperationLogCoverage(rows: AdOperationLogCoverageRow[]) {
  const labelCoverage = rows.filter((row) => row.frontendLabelExists).length;
  const snapshotCoverage = rows.filter((row) => row.snapshotRendererExists).length;
  const genericFallback = rows
    .filter((row) => !row.snapshotRendererExists)
    .map((row) => row.operationType);
  const localeGaps = rows
    .filter((row) => !row.trLocaleExists || !row.enLocaleExists)
    .map((row) => row.operationType);

  return {
    operationTypeCount: rows.length,
    labelCoverage,
    snapshotRendererCoverage: snapshotCoverage,
    genericFallbackOperations: genericFallback,
    localeGapOperations: localeGaps,
  };
}
