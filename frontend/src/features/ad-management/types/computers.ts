import type { AdManagementApiMessageFields } from "./common.ts";

export type AdComputerStatusFilter = "active" | "disabled" | "all";

export type AdComputerListItem = {
  id: string;
  name: string;
  samAccountName: string | null;
  dnsHostName: string | null;
  operatingSystem: string | null;
  distinguishedName: string;
  isEnabled: boolean;
  whenChanged: string | null;
};

export type AdComputerListResponse = {
  items: AdComputerListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type AdComputerOperatingSystemOptionsResponse = {
  items: string[];
};

export type AdComputerMemberOfItem = {
  distinguishedName: string;
  name: string | null;
  samAccountName: string | null;
};

export type AdComputerDetail = {
  id: string;
  name: string;
  cn: string | null;
  samAccountName: string | null;
  dnsHostName: string | null;
  distinguishedName: string;
  parentOuDistinguishedName: string | null;
  description: string | null;
  operatingSystem: string | null;
  operatingSystemVersion: string | null;
  operatingSystemServicePack: string | null;
  managedByDistinguishedName: string | null;
  managedByDisplayName: string | null;
  lastLogonAt: string | null;
  whenCreated: string | null;
  whenChanged: string | null;
  userAccountControl: number | null;
  isEnabled: boolean;
  primaryGroupId: number | null;
  memberOfCount: number;
  memberOf: AdComputerMemberOfItem[];
  memberOfTruncated: boolean;
};

export type GetAdComputersParams = {
  search?: string;
  status?: AdComputerStatusFilter;
  operatingSystem?: string;
  pageNumber?: number;
  pageSize?: number;
};

export type AdComputerAccountConfirmAction = "enable" | "disable";

export type AdComputerAccountOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  computer: AdComputerDetail | null;
};

export type DeleteAdComputerResponse = AdManagementApiMessageFields & {
  success: boolean;
  deletedComputerId: string | null;
  deletedComputerName: string | null;
  deletedDistinguishedName: string | null;
};

export type UpdateAdComputerRequest = {
  description: string | null;
};

export type MoveAdComputerOuRequest = {
  targetOuDistinguishedName: string;
};

export type AdComputerGroupMembershipItem = {
  id: string;
  distinguishedName: string;
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description: string | null;
  isDirect: boolean;
};

export type AdComputerGroupMembershipResponse = {
  computerId: string;
  name: string | null;
  samAccountName: string | null;
  dnsHostName: string | null;
  distinguishedName: string | null;
  groups: AdComputerGroupMembershipItem[];
};

export type AdComputerGroupCandidateItem = {
  distinguishedName: string;
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description: string | null;
};

export type AdComputerGroupCandidateSearchResponse = {
  items: AdComputerGroupCandidateItem[];
};

export type AdComputerGroupMutationRequest = {
  groupDistinguishedName: string;
};

export type AdComputerGroupOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  computerId: string | null;
  computerName: string | null;
  computerSamAccountName: string | null;
  groupDistinguishedName: string | null;
  groupName: string | null;
  groupDisplayName: string | null;
  groupSamAccountName: string | null;
};
