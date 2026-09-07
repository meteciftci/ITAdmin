import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  getAdNotificationChannelReadiness,
  getReadyAdNotificationChannels,
  isAdNotificationChannelReady,
  isEmailNotificationProviderReady,
  isSmsNotificationProviderReady,
} from "./is-notification-provider-ready.ts";
import { AD_NOTIFICATION_CHANNELS } from "./types.ts";
import type {
  EmailProviderSettings,
  SmsProviderSettings,
} from "../notification-providers/types.ts";

function createSmsSettings(
  overrides: Partial<SmsProviderSettings> = {},
): SmsProviderSettings {
  return {
    channel: "Sms",
    providerKey: "custom-http",
    availableProviders: ["custom-http", "teknomart"],
    isEnabled: true,
    displayName: null,
    sender: null,
    timeoutSeconds: 30,
    turkishCharacterMode: "Preserve",
    endpointUrl: "https://sms.example.com/send",
    method: "POST",
    contentType: "application/json",
    authType: "None",
    apiKeyName: null,
    headers: [],
    queryParameters: [],
    bodyTemplate: "{\"message\":\"{{message}}\"}",
    successStatusCodes: [200],
    successBodyContains: null,
    hasBasicPassword: false,
    hasBearerToken: false,
    hasApiKey: false,
    teknomartBaseUrl: null,
    teknomartDefaultSmsKind: "Single",
    teknomartSingleSmsTitle: null,
    teknomartEncoding: 0,
    teknomartValidity: 0,
    teknomartCommercial: false,
    teknomartPushWebhookUrl: null,
    hasTeknomartCredentials: false,
    lastValidatedAt: null,
    lastValidationStatus: "Ok",
    lastValidationMessage: null,
    ...overrides,
  };
}

function createEmailSettings(
  overrides: Partial<EmailProviderSettings> = {},
): EmailProviderSettings {
  return {
    channel: "Email",
    providerKey: "Smtp",
    isEnabled: true,
    displayName: null,
    host: "smtp.example.com",
    port: 587,
    useSsl: true,
    userName: "smtp-user",
    fromAddress: "noreply@example.com",
    fromDisplayName: null,
    timeoutSeconds: 30,
    hasPassword: true,
    lastValidatedAt: null,
    lastValidationStatus: "Ok",
    lastValidationMessage: null,
    ...overrides,
  };
}

describe("is-notification-provider-ready", () => {
  it("returns false when SMS provider is disabled or missing required fields", () => {
    assert.equal(isSmsNotificationProviderReady(createSmsSettings({ isEnabled: false })), false);
    assert.equal(isSmsNotificationProviderReady(createSmsSettings({ endpointUrl: null })), false);
    assert.equal(isSmsNotificationProviderReady(createSmsSettings({ bodyTemplate: null })), false);
  });

  it("requires SMS bearer credentials when bearer auth is configured", () => {
    assert.equal(
      isSmsNotificationProviderReady(
        createSmsSettings({ authType: "BearerToken", hasBearerToken: false }),
      ),
      false,
    );
    assert.equal(
      isSmsNotificationProviderReady(
        createSmsSettings({ authType: "BearerToken", hasBearerToken: true }),
      ),
      true,
    );
  });

  it("returns false when SMS last validation failed", () => {
    assert.equal(
      isSmsNotificationProviderReady(createSmsSettings({ lastValidationStatus: "Failed" })),
      false,
    );
  });

  it("evaluates Teknomart readiness against Teknomart fields, not Custom HTTP ones", () => {
    const teknomartReady = createSmsSettings({
      providerKey: "teknomart",
      endpointUrl: null,
      bodyTemplate: null,
      sender: "ITADMIN",
      teknomartBaseUrl: "https://api.teknomart.com.tr:9588",
      teknomartDefaultSmsKind: "Single",
      teknomartSingleSmsTitle: "ITAdmin-Portal",
      hasTeknomartCredentials: true,
    });
    assert.equal(isSmsNotificationProviderReady(teknomartReady), true);

    assert.equal(
      isSmsNotificationProviderReady(createSmsSettings({ ...teknomartReady, hasTeknomartCredentials: false })),
      false,
    );
    assert.equal(
      isSmsNotificationProviderReady(createSmsSettings({ ...teknomartReady, teknomartSingleSmsTitle: null })),
      false,
    );
  });

  it("returns false when email provider is disabled or missing required fields", () => {
    assert.equal(isEmailNotificationProviderReady(createEmailSettings({ isEnabled: false })), false);
    assert.equal(isEmailNotificationProviderReady(createEmailSettings({ host: null })), false);
    assert.equal(isEmailNotificationProviderReady(createEmailSettings({ fromAddress: null })), false);
  });

  it("requires email password when username is configured", () => {
    assert.equal(
      isEmailNotificationProviderReady(
        createEmailSettings({ userName: "smtp-user", hasPassword: false }),
      ),
      false,
    );
    assert.equal(
      isEmailNotificationProviderReady(
        createEmailSettings({ userName: null, hasPassword: false }),
      ),
      true,
    );
  });

  it("builds ready channel list from provider readiness", () => {
    const smsOnly = getAdNotificationChannelReadiness(
      createSmsSettings(),
      createEmailSettings({ isEnabled: false }),
    );
    assert.deepEqual(getReadyAdNotificationChannels(smsOnly), [AD_NOTIFICATION_CHANNELS.sms]);

    const emailOnly = getAdNotificationChannelReadiness(
      createSmsSettings({ isEnabled: false }),
      createEmailSettings(),
    );
    assert.deepEqual(getReadyAdNotificationChannels(emailOnly), [AD_NOTIFICATION_CHANNELS.email]);

    const bothReady = getAdNotificationChannelReadiness(
      createSmsSettings(),
      createEmailSettings(),
    );
    assert.deepEqual(getReadyAdNotificationChannels(bothReady), [
      AD_NOTIFICATION_CHANNELS.sms,
      AD_NOTIFICATION_CHANNELS.email,
    ]);

    assert.equal(isAdNotificationChannelReady(AD_NOTIFICATION_CHANNELS.sms, smsOnly), true);
    assert.equal(isAdNotificationChannelReady(AD_NOTIFICATION_CHANNELS.email, smsOnly), false);
  });
});
