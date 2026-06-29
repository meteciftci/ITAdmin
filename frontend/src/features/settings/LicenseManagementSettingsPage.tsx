import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { CheckboxField } from "@/components/common/CheckboxField";
import { FormError } from "@/components/common/FormError";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useAuthStore } from "@/features/auth/auth-store";
import {
  getLicenseManagementSettings,
  LICENSE_MANAGEMENT_SETTINGS_QUERY_KEY,
  updateLicenseManagementSettings,
} from "@/features/license-management/api";
import { getLicenseManagementApiErrorMessage } from "@/features/license-management/license-api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";

export function LicenseManagementSettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageSettings);
  const queryClient = useQueryClient();

  const [defaultCurrency, setDefaultCurrency] = useState("TRY");
  const [defaultVatIncluded, setDefaultVatIncluded] = useState(true);
  const [defaultRenewalReminderDays, setDefaultRenewalReminderDays] = useState("30");
  const [defaultRenewalRecipients, setDefaultRenewalRecipients] = useState("");
  const [defaultRenewalCcRecipients, setDefaultRenewalCcRecipients] = useState("");
  const [notes, setNotes] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const settingsQuery = useQuery({
    queryKey: LICENSE_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getLicenseManagementSettings,
  });

  useEffect(() => {
    if (!settingsQuery.data) {
      return;
    }

    /* eslint-disable react-hooks/set-state-in-effect -- hydrate settings form from query */
    setDefaultCurrency(settingsQuery.data.defaultCurrency);
    setDefaultVatIncluded(settingsQuery.data.defaultVatIncluded);
    setDefaultRenewalReminderDays(String(settingsQuery.data.defaultRenewalReminderDays));
    setDefaultRenewalRecipients(settingsQuery.data.defaultRenewalRecipients ?? "");
    setDefaultRenewalCcRecipients(settingsQuery.data.defaultRenewalCcRecipients ?? "");
    setNotes(settingsQuery.data.notes ?? "");
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [settingsQuery.data]);

  const saveMutation = useMutation({
    mutationFn: () =>
      updateLicenseManagementSettings({
        defaultCurrency: defaultCurrency.trim(),
        defaultVatIncluded,
        defaultRenewalReminderDays: Number(defaultRenewalReminderDays),
        defaultRenewalRecipients: defaultRenewalRecipients.trim() || null,
        defaultRenewalCcRecipients: defaultRenewalCcRecipients.trim() || null,
        notes: notes.trim() || null,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: LICENSE_MANAGEMENT_SETTINGS_QUERY_KEY });
      toast.success(t("settings:licenseManagement.messages.saveSuccess"));
      setErrorMessage(null);
    },
    onError: (error) => {
      setErrorMessage(
        getLicenseManagementApiErrorMessage(
          error,
          t,
          "settings:licenseManagement.messages.saveFailed",
        ),
      );
    },
  });

  const reminderDays = Number(defaultRenewalReminderDays);
  const canSave =
    canManage
    && defaultCurrency.trim().length > 0
    && Number.isFinite(reminderDays)
    && reminderDays >= 0
    && !saveMutation.isPending;

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("settings:pages.licenseManagement.title")}
        description={t("settings:pages.licenseManagement.description")}
      />

      {!canManage ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("settings:readOnlyNotice")}
        </p>
      ) : null}

      {settingsQuery.isLoading ? <LoadingState /> : null}

      <SectionCard title={t("settings:licenseManagement.formTitle")}>
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {t("settings:licenseManagement.renewalInfo")}
          </p>
          {errorMessage ? <FormError message={errorMessage} /> : null}
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="default-currency">{t("settings:licenseManagement.fields.defaultCurrency")}</Label>
              <Input
                id="default-currency"
                value={defaultCurrency}
                onChange={(e) => setDefaultCurrency(e.target.value)}
                disabled={!canManage || saveMutation.isPending}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="renewal-days">{t("settings:licenseManagement.fields.defaultRenewalReminderDays")}</Label>
              <Input
                id="renewal-days"
                type="number"
                min="0"
                value={defaultRenewalReminderDays}
                onChange={(e) => setDefaultRenewalReminderDays(e.target.value)}
                disabled={!canManage || saveMutation.isPending}
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="renewal-recipients">{t("settings:licenseManagement.fields.defaultRenewalRecipients")}</Label>
              <Textarea
                id="renewal-recipients"
                value={defaultRenewalRecipients}
                onChange={(e) => setDefaultRenewalRecipients(e.target.value)}
                disabled={!canManage || saveMutation.isPending}
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="renewal-cc">{t("settings:licenseManagement.fields.defaultRenewalCcRecipients")}</Label>
              <Textarea
                id="renewal-cc"
                value={defaultRenewalCcRecipients}
                onChange={(e) => setDefaultRenewalCcRecipients(e.target.value)}
                disabled={!canManage || saveMutation.isPending}
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="settings-notes">{t("settings:licenseManagement.fields.notes")}</Label>
              <Textarea
                id="settings-notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                disabled={!canManage || saveMutation.isPending}
              />
            </div>
            <CheckboxField
              id="default-vat-included"
              label={t("settings:licenseManagement.fields.defaultVatIncluded")}
              checked={defaultVatIncluded}
              onCheckedChange={(checked) => setDefaultVatIncluded(checked === true)}
              disabled={!canManage || saveMutation.isPending}
            />
          </div>
          {canManage ? (
            <div className="flex justify-end">
              <Button type="button" onClick={() => saveMutation.mutate()} disabled={!canSave}>
                {t("common:actions.save")}
              </Button>
            </div>
          ) : null}
        </div>
      </SectionCard>
    </section>
  );
}
