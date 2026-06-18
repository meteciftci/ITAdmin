import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { DataTable } from "@/components/common/data-table";
import { useClientDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { createNotificationTemplateColumns } from "@/features/notification-templates/notification-template-columns";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import {
  NOTIFICATION_TEMPLATES_QUERY_KEY,
  createNotificationTemplate,
  getNotificationTemplate,
  getNotificationTemplates,
  updateNotificationTemplate,
} from "@/features/notification-templates/api";
import type {
  NotificationTemplateListItem,
  SaveNotificationTemplateRequest,
} from "@/features/notification-templates/types";
import { useAuthStore } from "@/features/auth/auth-store";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";

type FormState = SaveNotificationTemplateRequest;

const emptyForm = (): FormState => ({
  moduleKey: "System",
  eventKey: "",
  channel: "Sms",
  name: "",
  isEnabled: true,
  subjectTemplate: "",
  bodyTemplate: "",
  description: "",
});

export function NotificationTemplatesPage() {
  const { t } = useTranslation(["notificationTemplates", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, PermissionCodes.NotificationTemplates.Update);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);

  const listQuery = useQuery({
    queryKey: NOTIFICATION_TEMPLATES_QUERY_KEY,
    queryFn: () => getNotificationTemplates(),
  });

  const items = listQuery.data ?? [];

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (editingId) {
        return updateNotificationTemplate(editingId, form);
      }
      return createNotificationTemplate(form);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_TEMPLATES_QUERY_KEY });
      toast.success(
        editingId
          ? t("notificationTemplates:messages.updateSuccess")
          : t("notificationTemplates:messages.createSuccess"),
      );
      setDialogOpen(false);
      setEditingId(null);
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("notificationTemplates:messages.saveFailed")));
    },
  });

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm());
    setDialogOpen(true);
  };

  const openEdit = useCallback(
    async (item: NotificationTemplateListItem) => {
      try {
        const template = await getNotificationTemplate(item.id);
        setEditingId(template.id);
        setForm({
          moduleKey: template.moduleKey,
          eventKey: template.eventKey,
          channel: template.channel,
          name: template.name,
          isEnabled: template.isEnabled,
          subjectTemplate: template.subjectTemplate ?? "",
          bodyTemplate: template.bodyTemplate,
          description: template.description ?? "",
        });
        setDialogOpen(true);
      } catch (error: unknown) {
        toast.error(getApiErrorMessage(error, t("notificationTemplates:messages.loadFailed")));
      }
    },
    [t],
  );

  const updateField = <K extends keyof FormState>(field: K, value: FormState[K]) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const columns = useMemo(
    () =>
      createNotificationTemplateColumns({
        t,
        canUpdate,
        onEdit: openEdit,
      }),
    [t, canUpdate, openEdit],
  );

  const table = useClientDataTable({
    data: items,
    columns,
    enablePagination: false,
  });

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("notificationTemplates:title")}
        description={t("notificationTemplates:description")}
      />

      {!canUpdate ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("notificationTemplates:readOnlyNotice")}
        </p>
      ) : null}

      <SectionCard title={t("notificationTemplates:sections.list")}>
        <div className="space-y-4">
          {canUpdate ? (
            <div className="flex justify-end">
              <Button type="button" onClick={openCreate}>
                {t("notificationTemplates:actions.add")}
              </Button>
            </div>
          ) : null}

          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && (listQuery.data?.length ?? 0) === 0 ? (
            <EmptyState title={t("notificationTemplates:empty.title")} />
          ) : null}

          {!listQuery.isLoading && items.length > 0 ? <DataTable table={table} /> : null}
        </div>
      </SectionCard>

      <Dialog open={dialogOpen}>
        <DialogContent className="max-w-2xl" onOpenChange={setDialogOpen}>
          <DialogHeader>
            <DialogTitle>
              {editingId
                ? t("notificationTemplates:dialog.editTitle")
                : t("notificationTemplates:dialog.createTitle")}
            </DialogTitle>
          </DialogHeader>
          <DialogBody>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label={t("notificationTemplates:fields.moduleKey")}>
              <Input
                value={form.moduleKey}
                onChange={(e) => updateField("moduleKey", e.target.value)}
                readOnly={!canUpdate}
              />
            </Field>
            <Field label={t("notificationTemplates:fields.eventKey")}>
              <Input
                value={form.eventKey}
                onChange={(e) => updateField("eventKey", e.target.value)}
                readOnly={!canUpdate}
              />
            </Field>
            <Field label={t("notificationTemplates:fields.channel")}>
              <Select
                value={form.channel}
                onChange={(e) => updateField("channel", e.target.value)}
                disabled={!canUpdate}
              >
                <option value="Sms">{t("common:channels.sms")}</option>
                <option value="Email">{t("common:channels.email")}</option>
              </Select>
            </Field>
            <Field label={t("notificationTemplates:fields.name")}>
              <Input
                value={form.name}
                onChange={(e) => updateField("name", e.target.value)}
                readOnly={!canUpdate}
              />
            </Field>
            <div className="flex items-center gap-2 md:col-span-2">
              <Switch
                checked={form.isEnabled}
                onCheckedChange={(checked) => updateField("isEnabled", checked)}
                disabled={!canUpdate}
              />
              <Label>{t("notificationTemplates:fields.isEnabled")}</Label>
            </div>
            <Field label={t("notificationTemplates:fields.subjectTemplate")} className="md:col-span-2">
              <Input
                value={form.subjectTemplate ?? ""}
                onChange={(e) => updateField("subjectTemplate", e.target.value)}
                readOnly={!canUpdate}
              />
            </Field>
            <Field label={t("notificationTemplates:fields.bodyTemplate")} className="md:col-span-2">
              <Textarea
                value={form.bodyTemplate}
                onChange={(e) => updateField("bodyTemplate", e.target.value)}
                rows={6}
                readOnly={!canUpdate}
              />
            </Field>
            <Field label={t("notificationTemplates:fields.description")} className="md:col-span-2">
              <Textarea
                value={form.description ?? ""}
                onChange={(e) => updateField("description", e.target.value)}
                rows={2}
                readOnly={!canUpdate}
              />
            </Field>
            <p className="text-xs text-muted-foreground md:col-span-2">
              {t("notificationTemplates:fields.variablesHint")}
            </p>
          </div>
          </DialogBody>
          {canUpdate ? (
            <DialogFooter>
              <Button variant="outline" onClick={() => setDialogOpen(false)}>
                {t("common:actions.cancel")}
              </Button>
              <Button
                onClick={() => saveMutation.mutate()}
                disabled={saveMutation.isPending}
              >
                {t("common:actions.save")}
              </Button>
            </DialogFooter>
          ) : null}
        </DialogContent>
      </Dialog>
    </section>
  );
}

function Field({
  label,
  children,
  className,
}: {
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={`space-y-2 ${className ?? ""}`}>
      <Label>{label}</Label>
      {children}
    </div>
  );
}
