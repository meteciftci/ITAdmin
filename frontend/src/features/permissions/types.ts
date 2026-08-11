export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type PermissionListItem = {
  id: string;
  module: string;
  name: string;
  code: string;
  description: string | null;
  isActive: boolean;
};

export type PermissionCatalog = {
  items: PermissionListItem[];
  totalCount: number;
};
