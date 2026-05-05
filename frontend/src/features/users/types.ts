export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type UserListItem = {
  id: string;
  userName: string;
  displayName: string;
  email: string;
  directorySource: string;
  directoryObjectId: string;
  nationalIdMasked: string;
  isActive: boolean;
  lastLoginAt: string | null;
  roles: string[];
};

export type UserDetail = {
  id: string;
  userName: string;
  displayName: string;
  email: string;
  directorySource: string;
  directoryObjectId: string;
  nationalIdMasked: string;
  isActive: boolean;
  lastLoginAt: string | null;
  roles: string[];
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type DirectoryLookupItem = {
  directoryObjectId: string;
  userName: string;
  displayName: string;
  email: string;
  nationalIdMasked: string;
  isAlreadyPortalUser: boolean;
};

export type DirectoryLookupResponse = {
  items: DirectoryLookupItem[];
};

export type CreateUserRequest = {
  directoryObjectId: string;
  isActive: boolean;
};

export type UpdateUserStatusRequest = {
  isActive: boolean;
};

export type UpdateUserRolesRequest = {
  roleIds: string[];
};

export type RoleListItem = {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystem: boolean;
  isActive: boolean;
  permissionCount: number;
};
