import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import {
  buildUpdateAdManagementSettingsPayload,
  defaultAdManagementNotificationSettings,
} from "@/features/ad-management/ad-management-settings-payload";
import {
  AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
  getAdAttributeMappings,
} from "@/features/ad-management/api";
import type {
  AdAttributeMapping,
  AdManagementNotificationRecipientSource,
  AdManagementNotificationSettings,
  AdManagementSettings,
  AdManagementUserCreatedNotificationSettings,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";
import { AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES } from "@/features/ad-management/types";
import {
  NOTIFICATION_TEMPLATES_QUERY_KEY,
  getNotificationTemplates,
} from "@/features/notification-templates/api";

type Props = {
  settings: AdManagementSettings | undefined;
  readOnly: boolean;
  isSaving: boolean;
  onSave: (payload: UpdateAdManagementSettingsRequest) => void;
};

type TemplateReadiness = "ready" | "missing" | "passive";

function resolveTemplateReadiness(
  templates: { channel: string; isEnabled: boolean }[],
  channel: "Sms" | "Email",
): TemplateReadiness {
  const match = templates.find((item) => item.channel === channel);
  if (!match) {
    return "missing";
  }

  return match.isEnabled ? "ready" : "passive";
}

function cloneNotificationSettings(
  settings: AdManagementNotificationSettings | undefined,
): AdManagementNotificationSettings {
  const base = settings ?? defaultAdManagementNotificationSettings();
  return {
    userCreated: {
      ...base.userCreated,
      smsRecipientSource: base.userCreated.smsRecipientSource
        ? { ...base.userCreated.smsRecipientSource }
        : null,
      emailRecipientSource: base.userCreated.emailRecipientSource
        ? { ...base.userCreated.emailRecipientSource }
        : null,
    },
  };
}

export function AdManagementNotificationsForm({
  settings,
  readOnly,
  isSaving,
  onSave,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const [notificationSettings, setNotificationSettings] = useState(() =>
    cloneNotificationSettings(settings?.notificationSettings),
  );

  const mappingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
    queryFn: getAdAttributeMappings,
  });

  const templatesQuery = useQuery({
    queryKey: [...NOTIFICATION_TEMPLATES_QUERY_KEY, "AdManagement", "UserCreated"],
    queryFn: () =>
      getNotificationTemplates({
        moduleKey: "AdManagement",
        eventKey: "UserCreated",
      }),
  });

  const enabledMappings = useMemo(
    () => (mappingsQuery.data ?? []).filter((mapping) => mapping.isEnabled),
    [mappingsQuery.data],
  );

  const userCreated = notificationSettings.userCreated;
  const channelsDisabled = !userCreated.isEnabled || readOnly;

  const smsTemplateStatus = resolveTemplateReadiness(templatesQuery.data ?? [], "Sms");
  const emailTemplateStatus = resolveTemplateReadiness(templatesQuery.data ?? [], "Email");

  function updateUserCreated(
    patch: Partial<AdManagementUserCreatedNotificationSettings>,
  ) {
    setNotificationSettings((prev) => ({
      userCreated: {
        ...prev.userCreated,
        ...patch,
      },
    }));
  }

  function updateRecipientSource(
    channel: "sms" | "email",
    source: AdManagementNotificationRecipientSource | null,
  ) {
    if (channel === "sms") {
      updateUserCreated({ smsRecipientSource: source });
      return;
    }

    updateUserCreated({ emailRecipientSource: source });
  }

  function handleNotificationEnabledChange(checked: boolean) {
    updateUserCreated({
      isEnabled: checked,
      ...(checked
        ? {}
        : {
            smsEnabled: false,
            emailEnabled: false,
          }),
    });
  }

  function validateBeforeSave(): string | null {
    if (!userCreated.isEnabled) {
      return null;
    }

    if (!userCreated.smsEnabled && !userCreated.emailEnabled) {
      return t("settings:adManagement.notifications.validation.channelRequired");
    }

    if (userCreated.smsEnabled && !userCreated.smsRecipientSource?.type) {
      return t("settings:adManagement.notifications.validation.smsRecipientRequired");
    }

    if (userCreated.emailEnabled && !userCreated.emailRecipientSource?.type) {
      return t("settings:adManagement.notifications.validation.emailRecipientRequired");
    }

    return null;
  }

  function handleSave() {
    if (!settings || readOnly) {
      return;
    }

    const validationError = validateBeforeSave();
    if (validationError) {
      return;
    }

    onSave(
      buildUpdateAdManagementSettingsPayload(settings, {
        notificationSettings,
      }),
    );
  }

  const validationError = validateBeforeSave();

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-base font-medium">
          {t("settings:adManagement.notifications.events.userCreated.title")}
        </h3>
        <p className="mt-1 text-sm text-muted-foreground">
          {t("settings:adManagement.notifications.events.userCreated.description")}
        </p>
      </div>

      <div className="space-y-4 rounded-md border p-4">
        <div className="flex items-center justify-between gap-4">
          <div className="space-y-0.5">
            <Label htmlFor="notification-enabled">
              {t("settings:adManagement.notifications.fields.notificationEnabled")}
            </Label>
          </div>
          <Switch
            id="notification-enabled"
            checked={userCreated.isEnabled}
            disabled={readOnly}
            onCheckedChange={handleNotificationEnabledChange}
          />
        </div>

        <div className="flex items-center justify-between gap-4">
          <Label htmlFor="sms-enabled">{t("settings:adManagement.notifications.fields.sms")}</Label>
          <Switch
            id="sms-enabled"
            checked={userCreated.smsEnabled}
            disabled={channelsDisabled}
            onCheckedChange={(checked) => updateUserCreated({ smsEnabled: checked })}
          />
        </div>

        {userCreated.smsEnabled ? (
          <RecipientSourceFields
            channel="sms"
            source={userCreated.smsRecipientSource}
            mappings={enabledMappings}
            disabled={channelsDisabled}
            onChange={(source) => updateRecipientSource("sms", source)}
          />
        ) : null}

        <div className="flex items-center justify-between gap-4">
          <Label htmlFor="email-enabled">
            {t("settings:adManagement.notifications.fields.email")}
          </Label>
          <Switch
            id="email-enabled"
            checked={userCreated.emailEnabled}
            disabled={channelsDisabled}
            onCheckedChange={(checked) => updateUserCreated({ emailEnabled: checked })}
          />
        </div>

        {userCreated.emailEnabled ? (
          <RecipientSourceFields
            channel="email"
            source={userCreated.emailRecipientSource}
            mappings={enabledMappings}
            disabled={channelsDisabled}
            onChange={(source) => updateRecipientSource("email", source)}
          />
        ) : null}

        <TemplateStatusSummary
          smsStatus={smsTemplateStatus}
          emailStatus={emailTemplateStatus}
        />

        {userCreated.isEnabled && (smsTemplateStatus === "missing" || emailTemplateStatus === "missing") ? (
          <p className="text-xs text-muted-foreground">
            {t("settings:adManagement.notifications.warnings.missingTemplate")}
          </p>
        ) : null}
      </div>

      {validationError && userCreated.isEnabled ? (
        <p className="text-sm text-destructive">{validationError}</p>
      ) : null}

      {!readOnly ? (
        <div className="flex justify-end">
          <Button type="button" disabled={isSaving} onClick={handleSave}>
            {t("settings:adManagement.notifications.actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

type RecipientSourceFieldsProps = {
  channel: "sms" | "email";
  source: AdManagementNotificationRecipientSource | null;
  mappings: AdAttributeMapping[];
  disabled: boolean;
  onChange: (source: AdManagementNotificationRecipientSource | null) => void;
};

function RecipientSourceFields({
  channel,
  source,
  mappings,
  disabled,
  onChange,
}: RecipientSourceFieldsProps) {
  const { t } = useTranslation(["settings"]);

  const typeOptions =
    channel === "sms"
      ? [
          {
            value: AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mappedAttribute,
            label: t("settings:adManagement.notifications.recipientTypes.mappedAttribute"),
          },
          {
            value: AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.adAttribute,
            label: t("settings:adManagement.notifications.recipientTypes.adAttribute"),
          },
        ]
      : [
          {
            value: AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.userPrincipalName,
            label: t("settings:adManagement.notifications.recipientTypes.userPrincipalName"),
          },
          {
            value: AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mailAttribute,
            label: t("settings:adManagement.notifications.recipientTypes.mailAttribute"),
          },
          {
            value: AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mappedAttribute,
            label: t("settings:adManagement.notifications.recipientTypes.mappedAttribute"),
          },
          {
            value: AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.adAttribute,
            label: t("settings:adManagement.notifications.recipientTypes.adAttribute"),
          },
        ];

  const selectedType = source?.type ?? "";

  function handleTypeChange(nextType: string) {
    if (!nextType) {
      onChange(null);
      return;
    }

    const needsValue =
      nextType === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mappedAttribute
      || nextType === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.adAttribute;

    onChange({
      type: nextType,
      value: needsValue ? source?.value ?? "" : null,
    });
  }

  return (
    <div className="space-y-3 rounded-md border border-dashed p-3">
      <div className="space-y-2">
        <Label>
          {channel === "sms"
            ? t("settings:adManagement.notifications.fields.smsRecipientSource")
            : t("settings:adManagement.notifications.fields.emailRecipientSource")}
        </Label>
        <Select
          value={selectedType}
          disabled={disabled}
          onChange={(event) => handleTypeChange(event.target.value)}
        >
          <option value="">{t("settings:adManagement.notifications.fields.selectSource")}</option>
          {typeOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </Select>
      </div>

      {selectedType === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mappedAttribute ? (
        <div className="space-y-2">
          <Label>{t("settings:adManagement.notifications.fields.mappedAttribute")}</Label>
          <Select
            value={source?.value ?? ""}
            disabled={disabled}
            onChange={(event) =>
              onChange({
                type: selectedType,
                value: event.target.value,
              })
            }
          >
            <option value="">{t("settings:adManagement.notifications.fields.selectMappedAttribute")}</option>
            {mappings.map((mapping) => (
              <option key={mapping.id} value={mapping.id}>
                {mapping.displayName}
              </option>
            ))}
          </Select>
        </div>
      ) : null}

      {selectedType === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.adAttribute ? (
        <div className="space-y-2">
          <Label>{t("settings:adManagement.notifications.fields.adAttribute")}</Label>
          <Input
            value={source?.value ?? ""}
            disabled={disabled}
            placeholder={t("settings:adManagement.notifications.placeholders.adAttribute")}
            onChange={(event) =>
              onChange({
                type: selectedType,
                value: event.target.value,
              })
            }
          />
        </div>
      ) : null}
    </div>
  );
}

type TemplateStatusSummaryProps = {
  smsStatus: TemplateReadiness;
  emailStatus: TemplateReadiness;
};

function TemplateStatusSummary({ smsStatus, emailStatus }: TemplateStatusSummaryProps) {
  const { t } = useTranslation(["settings"]);

  function label(status: TemplateReadiness): string {
    if (status === "ready") {
      return t("settings:adManagement.notifications.templateStatus.ready");
    }

    if (status === "passive") {
      return t("settings:adManagement.notifications.templateStatus.passive");
    }

    return t("settings:adManagement.notifications.templateStatus.missing");
  }

  return (
    <div className="space-y-2 rounded-md bg-muted/30 p-3 text-sm">
      <p className="font-medium">
        {t("settings:adManagement.notifications.fields.templateStatus")}
      </p>
      <p>
        {t("settings:adManagement.notifications.fields.smsTemplate")}: {label(smsStatus)}
      </p>
      <p>
        {t("settings:adManagement.notifications.fields.emailTemplate")}: {label(emailStatus)}
      </p>
    </div>
  );
}
