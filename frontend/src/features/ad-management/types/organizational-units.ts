import type { AdManagementApiMessageFields } from "./common.ts";

export type AdOrganizationalUnitManageListItem = {
  objectGuid: string;
  name: string | null;
  ou: string | null;
  displayName: string | null;
  displayLabel: string;
  distinguishedName: string;
  parentDistinguishedName: string | null;
  canonicalName: string;
  childOuCount: number | null;
  userCount: number | null;
  groupCount: number | null;
  computerCount: number | null;
};

export type AdOrganizationalUnitManageListResponse = {
  items: AdOrganizationalUnitManageListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type GetAdOrganizationalUnitsParams = {
  search?: string;
  pageNumber?: number;
  pageSize?: number;
};

export type AdOrganizationalUnitContentSummary = {
  childOuCount: number | null;
  userCount: number | null;
  groupCount: number | null;
  computerCount: number | null;
};

export type AdOrganizationalUnitChildListItem = {
  objectGuid: string;
  name: string | null;
  ou: string | null;
  displayName: string | null;
  displayLabel: string;
  distinguishedName: string;
  canonicalName: string;
};

export type AdOrganizationalUnitDetail = {
  objectGuid: string;
  name: string | null;
  ou: string | null;
  displayName: string | null;
  displayLabel?: string | null;
  distinguishedName: string;
  parentDistinguishedName: string | null;
  canonicalName: string;
  contentSummary: AdOrganizationalUnitContentSummary;
  childOrganizationalUnits: AdOrganizationalUnitChildListItem[];
};

export type CreateAdOrganizationalUnitRequest = {
  name: string;
  parentDistinguishedName: string;
};

export type RenameAdOrganizationalUnitRequest = {
  name: string;
};

export type MoveAdOrganizationalUnitRequest = {
  targetParentDistinguishedName: string;
};

export type CreateAdOrganizationalUnitResponse = AdManagementApiMessageFields & {
  success: boolean;
  organizationalUnit: AdOrganizationalUnitDetail | null;
};

export type RenameAdOrganizationalUnitResponse = AdManagementApiMessageFields & {
  success: boolean;
  organizationalUnit: AdOrganizationalUnitDetail | null;
  previousDistinguishedName: string | null;
};

export type MoveAdOrganizationalUnitResponse = AdManagementApiMessageFields & {
  success: boolean;
  organizationalUnit: AdOrganizationalUnitDetail | null;
  previousDistinguishedName: string | null;
  targetParentDistinguishedName: string | null;
};

export type DeleteAdOrganizationalUnitResponse = AdManagementApiMessageFields & {
  success: boolean;
  deletedOrganizationalUnitId: string | null;
};
