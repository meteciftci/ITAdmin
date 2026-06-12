export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type AdOperationLogStatus = "Succeeded" | "Failed" | "Skipped" | string;

export type AdOperationLogListItem = {
  id: string;
  createdAt: string;
  operationType: string;
  status: AdOperationLogStatus;
  targetObjectType: string | null;
  targetObjectGuid: string | null;
  targetDistinguishedName: string | null;
  targetSamAccountName: string | null;
  actorUserId: string | null;
  actorUserName: string | null;
  ipAddress: string | null;
  domainController: string | null;
  errorMessage: string | null;
  hasError: boolean;
  hasBeforeSnapshot: boolean;
  hasAfterSnapshot: boolean;
  hasRequestSummary: boolean;
};

export type AdOperationLogDetail = AdOperationLogListItem & {
  errorCode: string | null;
  requestSummaryJson: string | null;
  beforeSnapshotJson: string | null;
  afterSnapshotJson: string | null;
  userAgent: string | null;
  correlationId: string | null;
};

export type AdOperationLogFilters = {
  operationType?: string;
  status?: string;
  targetObjectType?: string;
  targetSamAccountName?: string;
  targetObjectGuid?: string;
  actorUserName?: string;
  domainController?: string;
  dateFrom?: string;
  dateTo?: string;
  pageNumber?: number;
  pageSize?: number;
};

export const AD_OPERATION_LOG_OPERATION_TYPES = [
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
  "ComputerEnable",
  "ComputerDisable",
  "ComputerUpdate",
  "ComputerMoveOu",
  "ComputerDelete",
] as const;

export const AD_OPERATION_LOG_STATUSES = [
  "Succeeded",
  "Failed",
  "Skipped",
] as const;
