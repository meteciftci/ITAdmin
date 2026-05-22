export type NotificationKeyValuePair = {
  key: string;
  value: string;
};

export type SmsProviderSettings = {
  channel: string;
  providerKey: string;
  isEnabled: boolean;
  displayName: string | null;
  sender: string | null;
  timeoutSeconds: number;
  endpointUrl: string | null;
  method: string;
  contentType: string;
  authType: string;
  apiKeyName: string | null;
  headers: NotificationKeyValuePair[];
  queryParameters: NotificationKeyValuePair[];
  bodyTemplate: string | null;
  successStatusCodes: number[];
  successBodyContains: string | null;
  turkishCharacterMode: string;
  hasBasicPassword: boolean;
  hasBearerToken: boolean;
  hasApiKey: boolean;
  lastValidatedAt: string | null;
  lastValidationStatus: string | null;
  lastValidationMessage: string | null;
};

export type EmailProviderSettings = {
  channel: string;
  providerKey: string;
  isEnabled: boolean;
  displayName: string | null;
  host: string | null;
  port: number;
  useSsl: boolean;
  userName: string | null;
  fromAddress: string | null;
  fromDisplayName: string | null;
  timeoutSeconds: number;
  hasPassword: boolean;
  lastValidatedAt: string | null;
  lastValidationStatus: string | null;
  lastValidationMessage: string | null;
};

export type UpdateSmsProviderSettingsRequest = {
  isEnabled: boolean;
  displayName?: string | null;
  sender?: string | null;
  timeoutSeconds: number;
  endpointUrl: string;
  method: string;
  contentType: string;
  authType: string;
  apiKeyName?: string | null;
  basicUserName?: string | null;
  basicPassword?: string | null;
  bearerToken?: string | null;
  apiKeyValue?: string | null;
  headers: NotificationKeyValuePair[];
  queryParameters: NotificationKeyValuePair[];
  bodyTemplate?: string | null;
  successStatusCodes: number[];
  successBodyContains?: string | null;
  turkishCharacterMode: string;
};

export type UpdateEmailProviderSettingsRequest = {
  isEnabled: boolean;
  displayName?: string | null;
  host: string;
  port: number;
  useSsl: boolean;
  userName?: string | null;
  password?: string | null;
  fromAddress: string;
  fromDisplayName?: string | null;
  timeoutSeconds: number;
};

export type TestSmsProviderRequest = {
  phoneNumber: string;
  message: string;
};

export type TestEmailProviderRequest = {
  recipientEmail: string;
  subject: string;
  body: string;
};

export type NotificationProviderOperationResponse = {
  message: string;
  providerSummary?: string | null;
};
