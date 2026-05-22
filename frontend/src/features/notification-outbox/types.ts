export type NotificationOutboxListItem = {
  id: string;
  channel: string;
  providerKey: string;
  recipientMasked: string;
  subject: string | null;
  status: string;
  attemptCount: number;
  maxAttempts: number;
  nextAttemptAt: string | null;
  lastAttemptAt: string | null;
  sentAt: string | null;
  relatedModule: string | null;
  relatedEvent: string | null;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
  createdAt: string;
  providerSummary: string | null;
  lastErrorMessage: string | null;
};

export type NotificationOutboxDetail = NotificationOutboxListItem & {
  body: string;
  lockedAt: string | null;
  lockedBy: string | null;
  correlationId: string | null;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type NotificationOutboxListQuery = {
  channel?: string;
  status?: string;
  relatedModule?: string;
  relatedEvent?: string;
  search?: string;
  pageNumber: number;
  pageSize: number;
};

export type PagedNotificationOutboxResponse = {
  items: NotificationOutboxListItem[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};
