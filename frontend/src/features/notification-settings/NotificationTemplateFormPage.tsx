import { useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Copy } from "lucide-react";
import { toast } from "sonner";
import { flushSync } from "react-dom";

import { LoadingState } from "@/components/common/LoadingState";
import { PageContainer } from "@/components/common/PageContainer";
import { PageHeader } from "@/components/common/PageHeader";
import {
  SettingsField,
  SettingsFormActions,
  SettingsSection,
  UnsavedChangesGuard,
} from "@/components/common/settings-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import {
  NOTIFICATION_TEMPLATE_CATALOG_QUERY_KEY,
  getNotificationTemplateCatalog,
} from "@/features/notification-settings/api";
import {
  getCatalogEventLabel,
  getCatalogModuleLabel,
  getCatalogVariableDescription,
  getCatalogVariableExample,
  getCatalogVariableLabel,
  getChannelLabel,
} from "@/features/notification-settings/catalog-labels";
import type {
  NotificationTemplateCatalog,
  NotificationTemplateCatalogEvent,
} from "@/features/notification-settings/types";
import { renderTemplatePreview } from "@/features/notification-settings/template-preview";
import {
  NOTIFICATION_TEMPLATES_QUERY_KEY,
  createNotificationTemplate,
  getNotificationTemplate,
  updateNotificationTemplate,
} from "@/features/notification-templates/api";
import type { SaveNotificationTemplateRequest } from "@/features/notification-templates/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

type NotificationTemplateFormPageProps = {
  mode: "create" | "edit";
};

const TEMPLATES_LIST_PATH = "/settings/notifications/templates";

function buildCreateInitialValues(catalog: NotificationTemplateCatalog): SaveNotificationTemplateRequest {
  const firstModule = catalog.modules[0];
  const firstEvent = firstModule?.events[0];

  return {
    moduleKey: firstModule?.key ?? "",
    eventKey: firstEvent?.key ?? "",
    channel: firstEvent?.supportedChannels[0] ?? "",
    name: "",
    isEnabled: true,
    subjectTemplate: "",
    bodyTemplate: "",
    description: "",
  };
}

function validateTemplate(form: SaveNotificationTemplateRequest, isSms: boolean) {
  const errors: Partial<Record<keyof SaveNotificationTemplateRequest, "required">> = {};
  if (!form.moduleKey) errors.moduleKey = "required";
  if (!form.eventKey) errors.eventKey = "required";
  if (!form.channel) errors.channel = "required";
  if (!form.name.trim()) errors.name = "required";
  if (!isSms && !form.subjectTemplate?.trim()) errors.subjectTemplate = "required";
  if (!form.bodyTemplate.trim()) errors.bodyTemplate = "required";
  return errors;
}

export function NotificationTemplateFormPage({ mode }: NotificationTemplateFormPageProps) {
  const { t } = useTranslation(["notificationSettings", "common"]);
  const { id } = useParams<{ id: string }>();

  const catalogQuery = useQuery({
    queryKey: NOTIFICATION_TEMPLATE_CATALOG_QUERY_KEY,
    queryFn: getNotificationTemplateCatalog,
  });

  const templateQuery = useQuery({
    queryKey: [...NOTIFICATION_TEMPLATES_QUERY_KEY, id],
    queryFn: () => getNotificationTemplate(id!),
    enabled: mode === "edit" && Boolean(id),
  });

  const isLoading =
    catalogQuery.isLoading || (mode === "edit" && (templateQuery.isLoading || !templateQuery.data));

  const pageTitle =
    mode === "create"
      ? t("notificationSettings:templates.createTitle")
      : t("notificationSettings:templates.editTitle");

  if (isLoading || !catalogQuery.data) {
    return (
      <PageContainer variant="form">
        <PageHeader title={pageTitle} />
        <LoadingState />
      </PageContainer>
    );
  }

  const initialValues: SaveNotificationTemplateRequest =
    mode === "edit" && templateQuery.data
      ? {
          moduleKey: templateQuery.data.moduleKey,
          eventKey: templateQuery.data.eventKey,
          channel: templateQuery.data.channel,
          name: templateQuery.data.name,
          isEnabled: templateQuery.data.isEnabled,
          subjectTemplate: templateQuery.data.subjectTemplate ?? "",
          bodyTemplate: templateQuery.data.bodyTemplate,
          description: templateQuery.data.description ?? "",
        }
      : buildCreateInitialValues(catalogQuery.data);

  const formKey = mode === "edit" ? `edit-${id}` : "create";

  return (
    <NotificationTemplateForm
      key={formKey}
      mode={mode}
      templateId={id}
      catalog={catalogQuery.data}
      initialValues={initialValues}
      pageTitle={pageTitle}
    />
  );
}

type NotificationTemplateFormProps = {
  mode: "create" | "edit";
  templateId?: string;
  catalog: NotificationTemplateCatalog;
  initialValues: SaveNotificationTemplateRequest;
  pageTitle: string;
};

function NotificationTemplateForm({
  mode,
  templateId,
  catalog,
  initialValues,
  pageTitle,
}: NotificationTemplateFormProps) {
  const { t } = useTranslation(["notificationSettings", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [form, setForm] = useState<SaveNotificationTemplateRequest>(initialValues);
  const [errors, setErrors] = useState<ReturnType<typeof validateTemplate>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [allowNavigation, setAllowNavigation] = useState(false);

  const selectedModule = useMemo(
    () => catalog.modules.find((m) => m.key === form.moduleKey) ?? null,
    [catalog, form.moduleKey],
  );

  const selectedEvent = useMemo((): NotificationTemplateCatalogEvent | null => {
    if (!selectedModule) {
      return null;
    }

    return selectedModule.events.find((e) => e.key === form.eventKey) ?? null;
  }, [selectedModule, form.eventKey]);

  const catalogMismatch = useMemo(() => {
    if (mode !== "edit") {
      return false;
    }

    return !catalog.modules.some(
      (module) =>
        module.key === initialValues.moduleKey &&
        module.events.some((evt) => evt.key === initialValues.eventKey),
    );
  }, [mode, catalog, initialValues.moduleKey, initialValues.eventKey]);

  const availableChannels = useMemo(() => {
    if (!selectedEvent) {
      return [] as string[];
    }

    return selectedEvent.supportedChannels;
  }, [selectedEvent]);

  const isSmsChannel = form.channel === "Sms";
  const isDirty = JSON.stringify(form) !== JSON.stringify(initialValues);

  const preview = useMemo(() => {
    const examples = new Map(
      (selectedEvent?.variables ?? []).map((variable) => [
        variable.key,
        getCatalogVariableExample(t, variable.key, variable.example) || `{{${variable.key}}}`,
      ]),
    );
    return {
      subject: renderTemplatePreview(form.subjectTemplate ?? "", examples),
      body: renderTemplatePreview(form.bodyTemplate, examples),
    };
  }, [form.bodyTemplate, form.subjectTemplate, selectedEvent, t]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      const payload: SaveNotificationTemplateRequest = {
        ...form,
        subjectTemplate: isSmsChannel ? null : form.subjectTemplate || null,
      };

      if (mode === "create") {
        return createNotificationTemplate(payload);
      }

      return updateNotificationTemplate(templateId!, payload);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_TEMPLATES_QUERY_KEY });
      toast.success(
        mode === "create"
          ? t("notificationSettings:templates.messages.createSuccess")
          : t("notificationSettings:templates.messages.updateSuccess"),
      );
      flushSync(() => setAllowNavigation(true));
      navigate(TEMPLATES_LIST_PATH);
    },
    onError: (error: unknown) => {
      setServerError(getApiErrorMessage(error, t("notificationSettings:templates.messages.saveFailed")));
    },
  });

  const updateField = <K extends keyof SaveNotificationTemplateRequest>(
    field: K,
    value: SaveNotificationTemplateRequest[K],
  ) => {
    setServerError(null);
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };

  const handleModuleChange = (moduleKey: string) => {
    const module = catalog.modules.find((m) => m.key === moduleKey);
    const firstEvent = module?.events[0];

    setForm((current) => ({
      ...current,
      moduleKey,
      eventKey: firstEvent?.key ?? "",
      channel: firstEvent?.supportedChannels[0] ?? "",
    }));
    setServerError(null);
  };

  const handleEventChange = (eventKey: string) => {
    const event = selectedModule?.events.find((e) => e.key === eventKey);

    setForm((current) => ({
      ...current,
      eventKey,
      channel: event?.supportedChannels[0] ?? current.channel,
    }));
    setServerError(null);
  };

  const copyVariable = async (variableKey: string) => {
    const token = `{{${variableKey}}}`;

    try {
      await navigator.clipboard.writeText(token);
      toast.success(t("notificationSettings:variables.copied"));
    } catch {
      toast.error(t("notificationSettings:variables.copyFailed"));
    }
  };

  const submit = () => {
    const nextErrors = validateTemplate(form, isSmsChannel);
    setErrors(nextErrors);
    setServerError(null);
    if (Object.keys(nextErrors).length === 0) saveMutation.mutate();
  };

  const fieldError = (field: keyof SaveNotificationTemplateRequest) =>
    errors[field] ? t("notificationSettings:validation.required") : undefined;
  const actionState = saveMutation.isPending
    ? "saving"
    : serverError
      ? "error"
      : isDirty
        ? "dirty"
        : "pristine";

  return (
    <PageContainer variant="form">
      <UnsavedChangesGuard
        when={isDirty && !allowNavigation && !saveMutation.isPending}
        title={t("notificationSettings:unsaved.title")}
        description={t("notificationSettings:unsaved.description")}
        leaveText={t("notificationSettings:unsaved.leave")}
        stayText={t("notificationSettings:unsaved.stay")}
      />
      <PageHeader title={pageTitle} />

      {catalogMismatch ? (
        <Alert><AlertTitle>{t("notificationSettings:templates.catalogMismatchTitle")}</AlertTitle><AlertDescription>{t("notificationSettings:templates.catalogMismatch")}</AlertDescription></Alert>
      ) : null}

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="space-y-6">
          <SettingsSection title={t("notificationSettings:sections.general")} description={t("notificationSettings:sections.generalDescription")}>
            {mode === "edit" ? <Alert><AlertTitle>{t("notificationSettings:templates.identityLockedTitle")}</AlertTitle><AlertDescription>{t("notificationSettings:templates.identityLockedDescription")}</AlertDescription></Alert> : null}
            <div className="grid gap-5 md:grid-cols-2">
              <SettingsField id="template-module" label={t("notificationSettings:fields.module")} error={fieldError("moduleKey")}>
                <Select
                  id="template-module"
                  value={form.moduleKey}
                  onChange={(event) => handleModuleChange(event.target.value)}
                  disabled={catalogMismatch || mode === "edit"}
                  aria-invalid={Boolean(errors.moduleKey)}
                >
                  <option value="">{t("notificationSettings:placeholders.selectModule")}</option>
                  {catalog.modules.map((module) => (
                    <option key={module.key} value={module.key}>
                      {getCatalogModuleLabel(t, module.key)}
                    </option>
                  ))}
                </Select>
              </SettingsField>

              <SettingsField id="template-event" label={t("notificationSettings:fields.event")} error={fieldError("eventKey")}>
                <Select
                  id="template-event"
                  value={form.eventKey}
                  onChange={(event) => handleEventChange(event.target.value)}
                  disabled={!form.moduleKey || catalogMismatch || mode === "edit"}
                  aria-invalid={Boolean(errors.eventKey)}
                >
                  <option value="">{t("notificationSettings:placeholders.selectEvent")}</option>
                  {selectedModule?.events.map((event) => (
                    <option key={event.key} value={event.key}>
                      {getCatalogEventLabel(t, form.moduleKey, event.key)}
                    </option>
                  ))}
                </Select>
              </SettingsField>

              <SettingsField id="template-channel" label={t("notificationSettings:fields.channel")} error={fieldError("channel")}>
                <Select
                  id="template-channel"
                  value={form.channel}
                  onChange={(event) => updateField("channel", event.target.value)}
                  disabled={!form.eventKey || catalogMismatch || mode === "edit"}
                  aria-invalid={Boolean(errors.channel)}
                >
                  <option value="">{t("notificationSettings:placeholders.selectChannel")}</option>
                  {availableChannels.map((channel) => (
                    <option key={channel} value={channel}>
                      {getChannelLabel(t, channel)}
                    </option>
                  ))}
                </Select>
              </SettingsField>

              <SettingsField id="template-name" label={t("notificationSettings:fields.templateName")} description={t("notificationSettings:fields.templateNameHint")} error={fieldError("name")}>
                <Input
                  id="template-name"
                  value={form.name}
                  onChange={(event) => updateField("name", event.target.value)}
                  aria-invalid={Boolean(errors.name)}
                />
              </SettingsField>

              <div className="flex items-center gap-3 rounded-lg border bg-muted/25 px-4 py-3 md:col-span-2">
                <Switch
                  id="template-enabled"
                  checked={form.isEnabled}
                  onCheckedChange={(checked) => updateField("isEnabled", checked)}
                />
                <label htmlFor="template-enabled" className="text-sm font-medium">{t("notificationSettings:fields.isEnabled")}</label>
              </div>
            </div>
          </SettingsSection>

          <SettingsSection title={t("notificationSettings:sections.content")} description={t("notificationSettings:sections.contentDescription")}>
            <div className="grid gap-5">
              {!isSmsChannel ? (
                <SettingsField id="template-subject" label={t("notificationSettings:fields.subjectTemplate")} error={fieldError("subjectTemplate")}>
                  <Input
                    id="template-subject"
                    value={form.subjectTemplate ?? ""}
                    onChange={(event) => updateField("subjectTemplate", event.target.value)}
                    aria-invalid={Boolean(errors.subjectTemplate)}
                  />
                </SettingsField>
              ) : null}

              <SettingsField id="template-body" label={t("notificationSettings:fields.bodyTemplate")} description={t("notificationSettings:fields.bodyTemplateHint")} error={fieldError("bodyTemplate")}>
                <Textarea
                  id="template-body"
                  value={form.bodyTemplate}
                  onChange={(event) => updateField("bodyTemplate", event.target.value)}
                  rows={12}
                  aria-invalid={Boolean(errors.bodyTemplate)}
                />
              </SettingsField>

              <SettingsField id="template-description" label={t("notificationSettings:fields.description")} optional optionalLabel={t("notificationSettings:fields.optional")}>
                <Textarea
                  id="template-description"
                  value={form.description ?? ""}
                  onChange={(event) => updateField("description", event.target.value)}
                  rows={2}
                />
              </SettingsField>
            </div>
          </SettingsSection>

          <SettingsFormActions state={actionState} stateLabel={t(`notificationSettings:saveStates.${actionState}`)} errorTitle={t("notificationSettings:saveStates.failedTitle")} errorMessage={serverError}>
            <Link to={TEMPLATES_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>{t("notificationSettings:actions.cancel")}</Link>
            <Button
              type="button"
              onClick={submit}
              disabled={saveMutation.isPending || catalogMismatch || !isDirty}
            >
              {saveMutation.isPending ? t("notificationSettings:actions.saving") : t("common:actions.save")}
            </Button>
          </SettingsFormActions>
        </div>

        <div className="space-y-6 lg:sticky lg:top-6">
        <SettingsSection title={t("notificationSettings:sections.variables")} description={t("notificationSettings:variables.formatHint")}>
          <p className="text-sm text-muted-foreground">
            {t("notificationSettings:variables.unknownHint")}
          </p>

          {!form.eventKey ? (
            <p className="text-sm text-muted-foreground">
              {t("notificationSettings:variables.selectEventFirst")}
            </p>
          ) : null}

          {form.eventKey && selectedEvent && selectedEvent.variables.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {t("notificationSettings:variables.empty")}
            </p>
          ) : null}

          <ul className="space-y-3">
            {selectedEvent?.variables.map((variable) => {
              const example = getCatalogVariableExample(t, variable.key, variable.example);

              return (
                <li key={variable.key} className="rounded-lg border bg-muted/20 p-3">
                  <div className="flex items-start justify-between gap-2">
                    <code className="text-sm font-medium">{`{{${variable.key}}}`}</code>
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      className="gap-1.5"
                      onClick={() => void copyVariable(variable.key)}
                    >
                      <Copy className="size-3.5" />
                      {t("notificationSettings:variables.copy")}
                    </Button>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {getCatalogVariableLabel(t, variable.key)}
                  </p>
                  {getCatalogVariableDescription(t, variable.key) ? (
                    <p className="mt-1 text-xs text-muted-foreground">
                      {getCatalogVariableDescription(t, variable.key)}
                    </p>
                  ) : null}
                  {example ? (
                    <p className="mt-1 text-xs">
                      {t("notificationSettings:variables.example")}: {example}
                    </p>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </SettingsSection>
        <SettingsSection title={t("notificationSettings:sections.preview")} description={t("notificationSettings:preview.description")}>
          {!isSmsChannel ? <div><p className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">{t("notificationSettings:preview.subject")}</p><p className="break-words text-sm font-medium">{preview.subject || t("notificationSettings:preview.empty")}</p></div> : null}
          <div><p className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">{t("notificationSettings:preview.body")}</p><pre className="whitespace-pre-wrap break-words rounded-lg border bg-muted/30 p-3 font-sans text-sm leading-6">{preview.body || t("notificationSettings:preview.empty")}</pre></div>
        </SettingsSection>
        </div>
      </div>
    </PageContainer>
  );
}
