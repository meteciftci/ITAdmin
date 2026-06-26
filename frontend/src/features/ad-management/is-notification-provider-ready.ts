import { AD_NOTIFICATION_CHANNELS } from "./types.ts";
import type {
  EmailProviderSettings,
  SmsProviderSettings,
} from "../notification-providers/types.ts";

const SUCCESS_VALIDATION_STATUS = "Ok";

const SMS_AUTH_TYPES = {
  none: "None",
  basic: "Basic",
  bearerToken: "BearerToken",
  apiKeyHeader: "ApiKeyHeader",
  apiKeyQuery: "ApiKeyQuery",
} as const;

export type AdNotificationChannelReadiness = {
  sms: boolean;
  email: boolean;
};

function normalizeValidationStatus(status: string | null | undefined): string {
  return status?.trim() ?? "";
}

function isSuccessfulValidationStatus(status: string | null | undefined): boolean {
  const normalized = normalizeValidationStatus(status);
  if (!normalized) {
    return false;
  }

  return normalized.toLowerCase() === SUCCESS_VALIDATION_STATUS.toLowerCase();
}

function hasFailedValidationStatus(status: string | null | undefined): boolean {
  const normalized = normalizeValidationStatus(status);
  if (!normalized) {
    return false;
  }

  return !isSuccessfulValidationStatus(normalized);
}

function finalizeProviderReadiness(
  fieldsReady: boolean,
  lastValidationStatus: string | null | undefined,
): boolean {
  if (!fieldsReady) {
    return false;
  }

  if (hasFailedValidationStatus(lastValidationStatus)) {
    return false;
  }

  return true;
}

function normalizeAuthType(authType: string | null | undefined): string {
  return authType?.trim() ?? "";
}

function hasSmsAuthCredentials(settings: SmsProviderSettings): boolean {
  const authType = normalizeAuthType(settings.authType);

  if (!authType || authType.toLowerCase() === SMS_AUTH_TYPES.none.toLowerCase()) {
    return true;
  }

  if (authType.toLowerCase() === SMS_AUTH_TYPES.basic.toLowerCase()) {
    return settings.hasBasicPassword;
  }

  if (authType.toLowerCase() === SMS_AUTH_TYPES.bearerToken.toLowerCase()) {
    return settings.hasBearerToken;
  }

  if (
    authType.toLowerCase() === SMS_AUTH_TYPES.apiKeyHeader.toLowerCase()
    || authType.toLowerCase() === SMS_AUTH_TYPES.apiKeyQuery.toLowerCase()
  ) {
    return Boolean(settings.apiKeyName?.trim()) && settings.hasApiKey;
  }

  return false;
}

export function isSmsNotificationProviderReady(
  settings?: SmsProviderSettings | null,
): boolean {
  if (!settings?.isEnabled) {
    return false;
  }

  const fieldsReady = Boolean(
    settings.endpointUrl?.trim()
      && settings.method?.trim()
      && settings.contentType?.trim()
      && settings.bodyTemplate?.trim()
      && hasSmsAuthCredentials(settings),
  );

  return finalizeProviderReadiness(fieldsReady, settings.lastValidationStatus);
}

export function isEmailNotificationProviderReady(
  settings?: EmailProviderSettings | null,
): boolean {
  if (!settings?.isEnabled) {
    return false;
  }

  const port = settings.port;
  const hasValidPort = Number.isFinite(port) && port > 0 && port <= 65535;
  const hasUserName = Boolean(settings.userName?.trim());
  const passwordReady = !hasUserName || settings.hasPassword;

  const fieldsReady = Boolean(
    settings.host?.trim()
      && hasValidPort
      && settings.fromAddress?.trim()
      && passwordReady,
  );

  return finalizeProviderReadiness(fieldsReady, settings.lastValidationStatus);
}

export function getAdNotificationChannelReadiness(
  smsSettings?: SmsProviderSettings | null,
  emailSettings?: EmailProviderSettings | null,
): AdNotificationChannelReadiness {
  return {
    sms: isSmsNotificationProviderReady(smsSettings),
    email: isEmailNotificationProviderReady(emailSettings),
  };
}

export function getReadyAdNotificationChannels(
  readiness: AdNotificationChannelReadiness,
): string[] {
  const channels: string[] = [];

  if (readiness.sms) {
    channels.push(AD_NOTIFICATION_CHANNELS.sms);
  }

  if (readiness.email) {
    channels.push(AD_NOTIFICATION_CHANNELS.email);
  }

  return channels;
}

export function isAdNotificationChannelReady(
  channel: string,
  readiness: AdNotificationChannelReadiness,
): boolean {
  if (channel === AD_NOTIFICATION_CHANNELS.sms) {
    return readiness.sms;
  }

  if (channel === AD_NOTIFICATION_CHANNELS.email) {
    return readiness.email;
  }

  return false;
}
