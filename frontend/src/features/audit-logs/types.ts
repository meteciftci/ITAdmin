export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type AuditLogListItem = {
  id: string;
  action: string;
  entityName: string;
  entityId: string | null;
  description: string | null;
  actorUserId: string | null;
  actorUserName: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
};

export type AuditLogFilterOptions = {
  actions: string[];
  entityNames: string[];
};

export type GetAuditLogsParams = {
  search?: string;
  action?: string;
  actions?: string[];
  entityName?: string;
  entityNames?: string[];
  actorUserId?: string;
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
};
