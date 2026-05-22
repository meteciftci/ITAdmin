import { apiClient } from "@/lib/api-client";

import type {
  EmailProviderSettings,
  NotificationProviderOperationResponse,
  SmsProviderSettings,
  TestEmailProviderRequest,
  TestSmsProviderRequest,
  UpdateEmailProviderSettingsRequest,
  UpdateSmsProviderSettingsRequest,
} from "@/features/notification-providers/types";

export const NOTIFICATION_SMS_SETTINGS_QUERY_KEY = ["notification-providers", "sms"] as const;
export const NOTIFICATION_EMAIL_SETTINGS_QUERY_KEY = ["notification-providers", "email"] as const;

export const getSmsProviderSettings = async (): Promise<SmsProviderSettings> => {
  const { data } = await apiClient.get<SmsProviderSettings>("/notification-providers/sms");
  return data;
};

export const getEmailProviderSettings = async (): Promise<EmailProviderSettings> => {
  const { data } = await apiClient.get<EmailProviderSettings>("/notification-providers/email");
  return data;
};

export const updateSmsProviderSettings = async (
  payload: UpdateSmsProviderSettingsRequest,
): Promise<SmsProviderSettings> => {
  const { data } = await apiClient.put<SmsProviderSettings>("/notification-providers/sms", payload);
  return data;
};

export const updateEmailProviderSettings = async (
  payload: UpdateEmailProviderSettingsRequest,
): Promise<EmailProviderSettings> => {
  const { data } = await apiClient.put<EmailProviderSettings>(
    "/notification-providers/email",
    payload,
  );
  return data;
};

export const testSmsProvider = async (
  payload: TestSmsProviderRequest,
): Promise<NotificationProviderOperationResponse> => {
  const { data } = await apiClient.post<NotificationProviderOperationResponse>(
    "/notification-providers/sms/test",
    payload,
  );
  return data;
};

export const testEmailProvider = async (
  payload: TestEmailProviderRequest,
): Promise<NotificationProviderOperationResponse> => {
  const { data } = await apiClient.post<NotificationProviderOperationResponse>(
    "/notification-providers/email/test",
    payload,
  );
  return data;
};
