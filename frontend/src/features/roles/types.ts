export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
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

export type RolePermissionItem = {
  id: string;
  module: string;
  name: string;
  code: string;
  description: string;
  isActive: boolean;
};

export type RoleDetail = {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystem: boolean;
  isActive: boolean;
  permissions: RolePermissionItem[];
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type CreateRoleRequest = {
  name: string;
  code: string;
  description: string;
  isActive: boolean;
};

export type UpdateRoleRequest = {
  name: string;
  description: string;
  isActive: boolean;
};

export type UpdateRoleStatusRequest = {
  isActive: boolean;
};

export type UpdateRolePermissionsRequest = {
  permissionIds: string[];
};
