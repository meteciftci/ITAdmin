import { apiClient } from "@/lib/api-client";

import type {
  NotificationOutboxDetail,
  NotificationOutboxListQuery,
  PagedNotificationOutboxResponse,
} from "@/features/notification-outbox/types";

export const NOTIFICATION_OUTBOX_QUERY_KEY = ["notification-outbox"] as const;

export const getNotificationOutboxItems = async (
  query: NotificationOutboxListQuery,
): Promise<PagedNotificationOutboxResponse> => {
  const { data } = await apiClient.get<PagedNotificationOutboxResponse>("/notification-outbox", {
    params: query,
  });
  return data;
};

export const getNotificationOutboxDetail = async (id: string): Promise<NotificationOutboxDetail> => {
  const { data } = await apiClient.get<NotificationOutboxDetail>(`/notification-outbox/${id}`);
  return data;
};

export const retryNotificationOutboxItem = async (id: string): Promise<NotificationOutboxDetail> => {
  const { data } = await apiClient.post<NotificationOutboxDetail>(`/notification-outbox/${id}/retry`);
  return data;
};

export const cancelNotificationOutboxItem = async (id: string): Promise<NotificationOutboxDetail> => {
  const { data } = await apiClient.post<NotificationOutboxDetail>(`/notification-outbox/${id}/cancel`);
  return data;
};
