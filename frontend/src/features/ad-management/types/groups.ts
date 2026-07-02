import type { AdManagementApiMessageFields } from "./common.ts";

export type AdGroupScope = "Global" | "DomainLocal" | "Universal" | "Unknown";

export type AdGroupMemberType = "User" | "Group" | "Computer" | "Unknown";

export type AdGroupListItem = {
  id: string;
  distinguishedName: string;
  displayName: string | null;
  name: string;
  cn: string | null;
  samAccountName: string | null;
  description: string | null;
  groupScope: AdGroupScope;
  securityEnabled: boolean;
  groupType: number | null;
};

export type AdGroupListResponse = {
  items: AdGroupListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type AdGroupMemberItem = {
  type: AdGroupMemberType;
  displayName: string | null;
  name: string | null;
  samAccountName: string | null;
  distinguishedName: string;
  description: string | null;
};

export type AdGroupDetail = {
  id: string;
  distinguishedName: string;
  displayName: string | null;
  name: string;
  cn: string | null;
  samAccountName: string | null;
  description: string | null;
  groupScope: AdGroupScope;
  securityEnabled: boolean;
  groupType: number | null;
  whenCreated: string | null;
  whenChanged: string | null;
  managedByDistinguishedName: string | null;
  managedByDisplayName: string | null;
  memberCount: number;
  memberOfCount: number;
  members: AdGroupMemberItem[];
  memberOf: AdGroupMemberItem[];
  membersTruncated: boolean;
  memberOfTruncated: boolean;
};

export type AdGroupMemberListTypeFilter = "all" | "user" | "group" | "computer";

export type AdGroupMemberListItem = {
  id: string | null;
  type: AdGroupMemberType;
  displayName: string | null;
  name: string | null;
  cn: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  dNSHostName: string | null;
  description: string | null;
  distinguishedName: string;
  isDirectMember: boolean;
};

export type AdGroupMembersListResponse = {
  items: AdGroupMemberListItem[];
  pageNumber: number;
  pageSize: number;
  memberCount: number;
  hasNextPage: boolean;
};

export type GetAdGroupMembersParams = {
  search?: string;
  type?: AdGroupMemberListTypeFilter;
  pageNumber?: number;
  pageSize?: number;
};

export type AdGroupMemberCandidateType = "user" | "group" | "computer";

export type AdGroupMemberCandidateItem = {
  id: string | null;
  type: AdGroupMemberType;
  displayName: string | null;
  name: string | null;
  cn: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  dNSHostName: string | null;
  description: string | null;
  distinguishedName: string;
  isAlreadyDirectMember: boolean;
  isEnabled: boolean | null;
};

export type AdGroupMemberCandidatesResponse = {
  items: AdGroupMemberCandidateItem[];
};

export type GetAdGroupMemberCandidatesParams = {
  search: string;
  types?: AdGroupMemberCandidateType[];
  pageSize?: number;
};

export type AddAdGroupMemberRequest = {
  memberDistinguishedName: string;
  memberType?: AdGroupMemberCandidateType;
};

export type RemoveAdGroupMemberRequest = {
  memberDistinguishedName: string;
};

export type AdGroupMemberOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  groupId: string | null;
  groupDistinguishedName: string | null;
  groupName: string | null;
  memberDistinguishedName: string | null;
  memberName: string | null;
};

export type GetAdGroupsParams = {
  search?: string;
  page?: number;
  pageSize?: number;
};

export type CreateAdGroupRequest = {
  displayName: string;
  name: string;
  samAccountName: string;
  description?: string | null;
  groupScope: AdGroupScope;
  targetOuDistinguishedName: string;
};

export type UpdateAdGroupRequest = {
  displayName: string;
  name: string;
  samAccountName: string;
  description?: string | null;
};

export type DeleteAdGroupResponse = AdManagementApiMessageFields & {
  success: boolean;
  deletedGroupId: string | null;
};

export type MoveAdGroupOuRequest = {
  targetOuDistinguishedName: string;
};

export type MoveAdGroupOuResponse = AdManagementApiMessageFields & {
  success: boolean;
  groupId: string;
  displayName: string | null;
  name: string | null;
  samAccountName: string | null;
  distinguishedName: string | null;
  previousDistinguishedName: string | null;
  targetOuDistinguishedName: string | null;
};

export type AdGroupSearchItem = {
  distinguishedName: string;
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description: string | null;
};

export type AdGroupSearchResponse = {
  items: AdGroupSearchItem[];
};
