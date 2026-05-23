import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import type {
  AdAttributeMapping,
  AdManagementNotificationRule,
  AdManagementNotificationRecipientSource,
} from "@/features/ad-management/types";
import {
  AD_NOTIFICATION_CHANNELS,
  AD_NOTIFICATION_EVENT_KEYS,
  AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES,
} from "@/features/ad-management/types";

export type NotificationRuleDialogMode = "create" | "edit";

type Props = {
  open: boolean;
  mode: NotificationRuleDialogMode;
  initialRule: AdManagementNotificationRule | null;
  existingRules: AdManagementNotificationRule[];
  mappings: AdAttributeMapping[];
  readOnly: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (rule: AdManagementNotificationRule) => void;
};

type FormState = {
  eventKey: string;
  channel: string;
  isEnabled: boolean;
  recipientType: string;
  recipientValue: string;
};

const EMPTY_FORM: FormState = {
  eventKey: "",
  channel: "",
  isEnabled: true,
  recipientType: "",
  recipientValue: "",
};

function buildRecipientSource(
  recipientType: string,
  recipientValue: string,
): AdManagementNotificationRecipientSource | null {
  if (!recipientType) {
    return null;
  }

  const needsValue =
    recipientType === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mappedAttribute
    || recipientType === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.adAttribute;

  return {
    type: recipientType,
    value: needsValue ? recipientValue || null : null,
  };
}

function recipientNeedsValue(type: string): boolean {
  return (
    type === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mappedAttribute
    || type === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.adAttribute
  );
}

function buildInitialForm(
  mode: NotificationRuleDialogMode,
  initialRule: AdManagementNotificationRule | null,
): FormState {
  if (mode === "edit" && initialRule) {
    return {
      eventKey: initialRule.eventKey,
      channel: initialRule.channel,
      isEnabled: initialRule.isEnabled,
      recipientType: initialRule.recipientSource?.type ?? "",
      recipientValue: initialRule.recipientSource?.value ?? "",
    };
  }

  return EMPTY_FORM;
}

type RuleDialogFormProps = Omit<Props, "open" | "onOpenChange">;

function AdManagementNotificationRuleDialogForm({
  mode,
  initialRule,
  existingRules,
  mappings,
  readOnly,
  onSubmit,
}: RuleDialogFormProps) {
  const { t } = useTranslation(["settings", "common"]);
  const [form, setForm] = useState<FormState>(() => buildInitialForm(mode, initialRule));
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const enabledMappings = useMemo(
    () => mappings.filter((mapping) => mapping.isEnabled),
    [mappings],
  );

  const recipientTypeOptions = useMemo(() => {
    const isSms = form.channel === AD_NOTIFICATION_CHANNELS.sms;
    if (isSms) {
      return [
        {
          value: AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mappedAttribute,
          label: t("settings:adManagement.notifications.recipientTypes.mappedAttribute"),
        },
        {
          value: AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.adAttribute,
          label: t("settings:adManagement.notifications.recipientTypes.adAttribute"),
        },
      ];
    }

    return [
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
  }, [form.channel, t]);

  const eventOptions = [
    {
      value: AD_NOTIFICATION_EVENT_KEYS.userCreated,
      label: t("settings:adManagement.notifications.events.userCreated.label"),
    },
    {
      value: AD_NOTIFICATION_EVENT_KEYS.userEnabled,
      label: t("settings:adManagement.notifications.events.userEnabled.label"),
    },
    {
      value: AD_NOTIFICATION_EVENT_KEYS.userDisabled,
      label: t("settings:adManagement.notifications.events.userDisabled.label"),
    },
    {
      value: AD_NOTIFICATION_EVENT_KEYS.userUnlocked,
      label: t("settings:adManagement.notifications.events.userUnlocked.label"),
    },
  ];

  function validate(): string | null {
    if (!form.eventKey || !form.channel) {
      return t("settings:adManagement.notifications.validation.requiredFields");
    }

    const duplicate = existingRules.some((rule) => {
      if (mode === "edit" && initialRule && rule.id === initialRule.id) {
        return false;
      }

      return (
        rule.eventKey === form.eventKey
        && rule.channel === form.channel
      );
    });

    if (duplicate) {
      return t("settings:adManagement.notifications.validation.duplicateRule");
    }

    if (!form.recipientType) {
      return t("settings:adManagement.notifications.validation.recipientRequired");
    }

    if (recipientNeedsValue(form.recipientType) && !form.recipientValue.trim()) {
      return t("settings:adManagement.notifications.validation.recipientValueRequired");
    }

    return null;
  }

  function handleSubmit() {
    const validationError = validate();
    if (validationError) {
      setErrorMessage(validationError);
      return;
    }

    const recipientSource = buildRecipientSource(form.recipientType, form.recipientValue);

    onSubmit({
      id: mode === "edit" && initialRule ? initialRule.id : crypto.randomUUID(),
      eventKey: form.eventKey,
      channel: form.channel,
      isEnabled: form.isEnabled,
      recipientSource,
    });
  }

  return (
    <div className="space-y-4">
          <div className="space-y-2">
            <Label>{t("settings:adManagement.notifications.fields.event")}</Label>
            <Select
              value={form.eventKey}
              disabled={readOnly || mode === "edit"}
              onChange={(event) =>
                setForm((prev) => ({ ...prev, eventKey: event.target.value }))}
            >
              <option value="">{t("settings:adManagement.notifications.fields.selectEvent")}</option>
              {eventOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </div>

          <div className="space-y-2">
            <Label>{t("settings:adManagement.notifications.fields.channel")}</Label>
            <Select
              value={form.channel}
              disabled={readOnly || mode === "edit"}
              onChange={(event) =>
                setForm((prev) => ({
                  ...prev,
                  channel: event.target.value,
                  recipientType: "",
                  recipientValue: "",
                }))}
            >
              <option value="">{t("settings:adManagement.notifications.fields.selectChannel")}</option>
              <option value={AD_NOTIFICATION_CHANNELS.sms}>
                {t("settings:adManagement.notifications.channels.sms")}
              </option>
              <option value={AD_NOTIFICATION_CHANNELS.email}>
                {t("settings:adManagement.notifications.channels.email")}
              </option>
            </Select>
          </div>

          {form.channel ? (
            <div className="space-y-2">
              <Label>{t("settings:adManagement.notifications.fields.recipientSourceType")}</Label>
              <Select
                value={form.recipientType}
                disabled={readOnly}
                onChange={(event) =>
                  setForm((prev) => ({
                    ...prev,
                    recipientType: event.target.value,
                    recipientValue: recipientNeedsValue(event.target.value)
                      ? prev.recipientValue
                      : "",
                  }))}
              >
                <option value="">{t("settings:adManagement.notifications.fields.selectSource")}</option>
                {recipientTypeOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </Select>
            </div>
          ) : null}

          {form.recipientType === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.mappedAttribute ? (
            <div className="space-y-2">
              <Label>{t("settings:adManagement.notifications.fields.mappedAttribute")}</Label>
              <Select
                value={form.recipientValue}
                disabled={readOnly}
                onChange={(event) =>
                  setForm((prev) => ({ ...prev, recipientValue: event.target.value }))}
              >
                <option value="">{t("settings:adManagement.notifications.fields.selectMappedAttribute")}</option>
                {enabledMappings.map((mapping) => (
                  <option key={mapping.id} value={mapping.id}>
                    {mapping.displayName}
                  </option>
                ))}
              </Select>
            </div>
          ) : null}

          {form.recipientType === AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES.adAttribute ? (
            <div className="space-y-2">
              <Label>{t("settings:adManagement.notifications.fields.adAttribute")}</Label>
              <Input
                value={form.recipientValue}
                disabled={readOnly}
                placeholder={t("settings:adManagement.notifications.placeholders.adAttribute")}
                onChange={(event) =>
                  setForm((prev) => ({ ...prev, recipientValue: event.target.value }))}
              />
            </div>
          ) : null}

          <div className="flex items-center justify-between gap-4">
            <Label htmlFor="rule-enabled">{t("settings:adManagement.notifications.fields.status")}</Label>
            <Switch
              id="rule-enabled"
              checked={form.isEnabled}
              disabled={readOnly}
              onCheckedChange={(checked) => setForm((prev) => ({ ...prev, isEnabled: checked }))}
            />
          </div>

          {errorMessage ? <p className="text-sm text-destructive">{errorMessage}</p> : null}

      {!readOnly ? (
        <div className="flex justify-end">
          <Button type="button" onClick={handleSubmit}>
            {t("common:actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

export function AdManagementNotificationRuleDialog({
  open,
  mode,
  initialRule,
  existingRules,
  mappings,
  readOnly,
  onOpenChange,
  onSubmit,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const formKey = open ? `${mode}-${initialRule?.id ?? "create"}` : "closed";

  if (!open) {
    return null;
  }

  return (
    <Dialog open={open}>
      <DialogContent className="max-w-lg" onOpenChange={onOpenChange}>
        <DialogHeader>
          <DialogTitle>
            {mode === "create"
              ? t("settings:adManagement.notifications.actions.add")
              : t("settings:adManagement.notifications.actions.edit")}
          </DialogTitle>
        </DialogHeader>

        <AdManagementNotificationRuleDialogForm
          key={formKey}
          mode={mode}
          initialRule={initialRule}
          existingRules={existingRules}
          mappings={mappings}
          readOnly={readOnly}
          onSubmit={(rule) => {
            onSubmit(rule);
            onOpenChange(false);
          }}
        />

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {t("common:actions.cancel")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
