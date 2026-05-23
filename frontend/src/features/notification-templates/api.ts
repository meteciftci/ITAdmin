import { apiClient } from "@/lib/api-client";

import type {
  NotificationTemplate,
  NotificationTemplateListItem,
  SaveNotificationTemplateRequest,
} from "@/features/notification-templates/types";

export const NOTIFICATION_TEMPLATES_QUERY_KEY = ["notification-templates"] as const;

export const getNotificationTemplates = async (params?: {
  moduleKey?: string;
  eventKey?: string;
  channel?: string;
}): Promise<NotificationTemplateListItem[]> => {
  const { data } = await apiClient.get<NotificationTemplateListItem[]>("/notification-templates", {
    params,
  });
  return data;
};

export const getNotificationTemplate = async (id: string): Promise<NotificationTemplate> => {
  const { data } = await apiClient.get<NotificationTemplate>(`/notification-templates/${id}`);
  return data;
};

export const createNotificationTemplate = async (
  payload: SaveNotificationTemplateRequest,
): Promise<NotificationTemplate> => {
  const { data } = await apiClient.post<NotificationTemplate>("/notification-templates", payload);
  return data;
};

export const updateNotificationTemplate = async (
  id: string,
  payload: SaveNotificationTemplateRequest,
): Promise<NotificationTemplate> => {
  const { data } = await apiClient.put<NotificationTemplate>(`/notification-templates/${id}`, payload);
  return data;
};

export const updateNotificationTemplateStatus = async (
  id: string,
  isEnabled: boolean,
): Promise<NotificationTemplate> => {
  const { data } = await apiClient.patch<NotificationTemplate>(
    `/notification-templates/${id}/status`,
    { isEnabled },
  );
  return data;
};
