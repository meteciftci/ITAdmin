export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type PermissionListItem = {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isActive: boolean;
  group?: string | null;
  module?: string | null;
  category?: string | null;
};
