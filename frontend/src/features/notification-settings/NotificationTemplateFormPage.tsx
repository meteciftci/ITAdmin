import { useMemo, useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Copy } from "lucide-react";
import { toast } from "sonner";

import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
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
      <section className="space-y-4">
        <PageHeader title={pageTitle} />
        <LoadingState />
      </section>
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
      navigate(TEMPLATES_LIST_PATH);
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("notificationSettings:templates.messages.saveFailed")));
    },
  });

  const updateField = <K extends keyof SaveNotificationTemplateRequest>(
    field: K,
    value: SaveNotificationTemplateRequest[K],
  ) => {
    setForm((current) => ({ ...current, [field]: value }));
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
  };

  const handleEventChange = (eventKey: string) => {
    const event = selectedModule?.events.find((e) => e.key === eventKey);

    setForm((current) => ({
      ...current,
      eventKey,
      channel: event?.supportedChannels[0] ?? current.channel,
    }));
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

  const canSubmit =
    Boolean(form.moduleKey) &&
    Boolean(form.eventKey) &&
    Boolean(form.channel) &&
    Boolean(form.name.trim()) &&
    Boolean(form.bodyTemplate.trim());

  return (
    <section className="space-y-4">
      <PageHeader title={pageTitle} />

      {catalogMismatch ? (
        <p className="rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-900 dark:text-amber-200">
          {t("notificationSettings:templates.catalogMismatch")}
        </p>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div className="space-y-4">
          <SectionCard title={t("notificationSettings:sections.general")}>
            <div className="grid gap-4 md:grid-cols-2">
              <FormField label={t("notificationSettings:fields.module")}>
                <Select
                  value={form.moduleKey}
                  onChange={(event) => handleModuleChange(event.target.value)}
                  disabled={catalogMismatch}
                >
                  <option value="">{t("notificationSettings:placeholders.selectModule")}</option>
                  {catalog.modules.map((module) => (
                    <option key={module.key} value={module.key}>
                      {getCatalogModuleLabel(t, module.key)}
                    </option>
                  ))}
                </Select>
              </FormField>

              <FormField label={t("notificationSettings:fields.event")}>
                <Select
                  value={form.eventKey}
                  onChange={(event) => handleEventChange(event.target.value)}
                  disabled={!form.moduleKey || catalogMismatch}
                >
                  <option value="">{t("notificationSettings:placeholders.selectEvent")}</option>
                  {selectedModule?.events.map((event) => (
                    <option key={event.key} value={event.key}>
                      {getCatalogEventLabel(t, form.moduleKey, event.key)}
                    </option>
                  ))}
                </Select>
              </FormField>

              <FormField label={t("notificationSettings:fields.channel")}>
                <Select
                  value={form.channel}
                  onChange={(event) => updateField("channel", event.target.value)}
                  disabled={!form.eventKey || catalogMismatch}
                >
                  <option value="">{t("notificationSettings:placeholders.selectChannel")}</option>
                  {availableChannels.map((channel) => (
                    <option key={channel} value={channel}>
                      {getChannelLabel(t, channel)}
                    </option>
                  ))}
                </Select>
              </FormField>

              <FormField label={t("notificationSettings:fields.templateName")}>
                <Input
                  value={form.name}
                  onChange={(event) => updateField("name", event.target.value)}
                />
              </FormField>

              <div className="flex items-center gap-2 md:col-span-2">
                <Switch
                  checked={form.isEnabled}
                  onCheckedChange={(checked) => updateField("isEnabled", checked)}
                />
                <Label>{t("notificationSettings:fields.isEnabled")}</Label>
              </div>
            </div>
          </SectionCard>

          <SectionCard title={t("notificationSettings:sections.content")}>
            <div className="grid gap-4">
              {!isSmsChannel ? (
                <FormField label={t("notificationSettings:fields.subjectTemplate")}>
                  <Input
                    value={form.subjectTemplate ?? ""}
                    onChange={(event) => updateField("subjectTemplate", event.target.value)}
                  />
                </FormField>
              ) : null}

              <FormField label={t("notificationSettings:fields.bodyTemplate")}>
                <Textarea
                  value={form.bodyTemplate}
                  onChange={(event) => updateField("bodyTemplate", event.target.value)}
                  rows={8}
                />
              </FormField>

              <FormField label={t("notificationSettings:fields.description")}>
                <Textarea
                  value={form.description ?? ""}
                  onChange={(event) => updateField("description", event.target.value)}
                  rows={2}
                />
              </FormField>
            </div>
          </SectionCard>

          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              onClick={() => saveMutation.mutate()}
              disabled={!canSubmit || saveMutation.isPending || catalogMismatch}
            >
              {t("common:actions.save")}
            </Button>
            <Link
              to={TEMPLATES_LIST_PATH}
              className={cn(buttonVariants({ variant: "outline" }))}
            >
              {t("notificationSettings:actions.cancel")}
            </Link>
          </div>
        </div>

        <SectionCard title={t("notificationSettings:sections.variables")}>
          <p className="mb-3 text-xs text-muted-foreground">
            {t("notificationSettings:variables.formatHint")}
          </p>
          <p className="mb-4 text-xs text-muted-foreground">
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
                <li key={variable.key} className="rounded-md border p-3">
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
        </SectionCard>
      </div>
    </section>
  );
}

function FormField({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
