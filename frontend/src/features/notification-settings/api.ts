import { apiClient } from "@/lib/api-client";

import type { NotificationTemplateCatalog } from "@/features/notification-settings/types";

export const NOTIFICATION_TEMPLATE_CATALOG_QUERY_KEY = ["notification-templates", "catalog"] as const;

export const getNotificationTemplateCatalog = async (): Promise<NotificationTemplateCatalog> => {
  const { data } = await apiClient.get<NotificationTemplateCatalog>("/notification-templates/catalog");
  return data;
};
