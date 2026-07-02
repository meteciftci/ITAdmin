import type { AdManagementApiMessageFields } from "./common.ts";

export type AdDeletedObjectType = "User" | "Group" | "Computer" | "Unknown";

export type AdDeletedObjectTypeFilter = "all" | "user" | "group" | "computer";

export type AdDeletedObjectListItem = {
  id: string;
  objectType: AdDeletedObjectType;
  name: string | null;
  displayName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  distinguishedName: string;
  lastKnownParent: string | null;
  whenChanged: string | null;
  deletedAt: string | null;
};

export type AdDeletedObjectListResponse = {
  items: AdDeletedObjectListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type AdDeletedObjectDetail = {
  id: string;
  objectType: AdDeletedObjectType;
  name: string | null;
  displayName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  description: string | null;
  distinguishedName: string;
  lastKnownParent: string | null;
  lastKnownRdn: string | null;
  objectClass: string[];
  objectSid: string | null;
  whenCreated: string | null;
  whenChanged: string | null;
  deletedAt: string | null;
  mail: string | null;
  department: string | null;
  dnsHostName: string | null;
  operatingSystem: string | null;
  memberOfCount: number;
  memberOf: string[];
  memberOfTruncated: boolean;
  additionalAttributes: Record<string, string>;
};

export type GetAdDeletedObjectsParams = {
  search?: string;
  type?: AdDeletedObjectTypeFilter;
  pageNumber?: number;
  pageSize?: number;
  includeAll?: boolean;
};

export type AdDeletedObjectRestoreResponse = AdManagementApiMessageFields & {
  success: boolean;
  restoredObjectId: string | null;
  restoredObjectType: AdDeletedObjectType | null;
  restoredName: string | null;
  restoredSamAccountName: string | null;
  restoredDistinguishedName: string | null;
  restoredLastKnownParent: string | null;
};
