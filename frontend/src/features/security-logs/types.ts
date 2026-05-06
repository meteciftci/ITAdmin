export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type SecurityLogListItem = {
  id: string;
  eventType: string;
  severity: string;
  userId: string | null;
  userName: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  description: string | null;
  createdAt: string;
};

export type SecurityLogFilterOptions = {
  eventTypes: string[];
  severities: string[];
};

export type GetSecurityLogsParams = {
  search?: string;
  eventTypes?: string[];
  severities?: string[];
  userId?: string;
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
};
